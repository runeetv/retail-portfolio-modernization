using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>
/// Staff-assisted order creation, lookup, cancellation (with authorized override),
/// and read-only shipment status (SCENARIO 2, 3, 6 - OrderDesk side).
/// </summary>
public class OrderService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public OrderService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public sealed record NewOrderLine(int ProductId, int Quantity);

    /// <summary>
    /// Creates a staff-assisted order directly (not via quote). Rechecks availability
    /// and mandatory customer info. (SCENARIO 2: OrderDesk path for order creation.)
    /// </summary>
    public async Task<OperationResult<int>> CreateStaffOrderAsync(int customerId, int agentId, IReadOnlyList<NewOrderLine> lines, CancellationToken ct = default)
    {
        if (lines.Count == 0) return OperationResult<int>.Fail("Order must have at least one line.");

        await using var db = _ctx();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (customer is null) return OperationResult<int>.Fail("Customer not found.");
        if (!OrderDeskRules.CustomerCanOrder(customer))
            return OperationResult<int>.Fail("Customer is missing mandatory contact/shipping information.");
        if (!await db.SalesAgents.AnyAsync(a => a.SalesAgentId == agentId, ct))
            return OperationResult<int>.Fail("Sales agent not found.");

        // Availability check before accepting the order (OrderDesk's variant of inventory rule).
        foreach (var l in lines)
        {
            if (l.Quantity <= 0) return OperationResult<int>.Fail("Line quantities must be positive.");
            var inv = await db.Inventory.FirstOrDefaultAsync(i => i.ProductId == l.ProductId, ct);
            if (inv is null || (inv.QuantityOnHand - inv.QuantityAllocated) < l.Quantity)
                return OperationResult<int>.Fail($"Insufficient stock for product {l.ProductId}.");
        }

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customerId,
            OrderStatus = "Confirmed",
            OrderSource = "OrderDesk",
            PlacedByAgentId = agentId,
            PlacedUtc = DateTime.UtcNow
        };

        decimal sub = 0;
        foreach (var l in lines)
        {
            var product = await db.Products.FirstAsync(p => p.ProductId == l.ProductId, ct);
            order.Lines.Add(new OrderLine { ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = product.UnitPrice });
            sub += product.UnitPrice * l.Quantity;
        }
        order.SubTotal = sub;
        order.DiscountTotal = 0;
        order.GrandTotal = sub;

        db.Orders.Add(order);

        // Execution strategy wrapper: compatible with EnableRetryOnFailure.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return OperationResult<int>.Ok(order.OrderId);
    }

    public async Task<Order?> GetAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.Orders.AsNoTracking().Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
    }

    public async Task<List<Order>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        await using var db = _ctx();
        IQueryable<Order> q = db.Orders.AsNoTracking().Include(o => o.Customer);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.OrderStatus == status);
        return await q.OrderByDescending(o => o.PlacedUtc).Take(200).ToListAsync(ct);
    }

    /// <summary>
    /// Cancels an order. Uses SQL usp_EvaluateCancellationEligibility for the staff rule.
    /// When the order is NOT normally eligible, an authorized agent (CanOverrideCancellation)
    /// may force cancellation with a justification; an audit CancellationOverride is written.
    /// </summary>
    public async Task<OperationResult> CancelOrderAsync(int orderId, int agentId, string? reason, bool forceOverride = false, string? overrideJustification = null, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return OperationResult.Fail("Order not found.");
        if (order.OrderStatus == "Cancelled") return OperationResult.Fail("Order already cancelled.");

        var (eligible, evalReason) = await EvaluateEligibilityAsync(db, orderId, ct);

        if (!eligible)
        {
            if (!forceOverride)
                return OperationResult.Fail($"Not eligible for cancellation: {evalReason}");

            var agent = await db.SalesAgents.FirstOrDefaultAsync(a => a.SalesAgentId == agentId, ct);
            if (agent is null || !agent.CanOverrideCancellation)
                return OperationResult.Fail("Agent is not authorized to override cancellation restrictions.");
            if (string.IsNullOrWhiteSpace(overrideJustification))
                return OperationResult.Fail("Override requires a justification.");

            db.CancellationOverrides.Add(new CancellationOverride
            {
                OrderId = orderId,
                SalesAgentId = agentId,
                PreviousStatus = order.OrderStatus,
                Justification = overrideJustification,
                CreatedUtc = DateTime.UtcNow
            });
        }

        order.OrderStatus = "Cancelled";
        order.CancelReason = reason;
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>Calls the SQL stored proc that owns the staff cancellation rule.</summary>
    private static async Task<(bool eligible, string reason)> EvaluateEligibilityAsync(OrderDeskDbContext db, int orderId, CancellationToken ct)
    {
        var conn = (SqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_EvaluateCancellationEligibility";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@OrderId", orderId));
        var pElig = new SqlParameter("@IsEligible", System.Data.SqlDbType.Bit) { Direction = System.Data.ParameterDirection.Output };
        var pReason = new SqlParameter("@Reason", System.Data.SqlDbType.NVarChar, 200) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pElig); cmd.Parameters.Add(pReason);
        await cmd.ExecuteNonQueryAsync(ct);
        return (pElig.Value is bool b && b, pReason.Value as string ?? "");
    }

    public async Task<(bool eligible, string reason)> CheckCancellationEligibilityAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await EvaluateEligibilityAsync(db, orderId, ct);
    }

    /// <summary>Read-only shipment status for customer-service lookups (SCENARIO 6).</summary>
    public async Task<List<ShipmentStatusView>> GetShipmentsAsync(int orderId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.Shipments.AsNoTracking().Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.ShipmentId).ToListAsync(ct);
    }
}
