using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Own order history + customer-initiated cancellation (SCENARIO 3, CustomerPortal side).
/// Ownership enforced: callers pass the signed-in customerId and we filter on it.
/// </summary>
public class OrderHistoryService
{
    private readonly PortalDbContext _db;
    public OrderHistoryService(PortalDbContext db) => _db = db;

    public async Task<List<CustomerOrder>> GetOwnOrdersAsync(int customerId, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedUtc).Take(100).ToListAsync(ct);

    /// <summary>Returns the order only if it belongs to the given customer (ownership check).</summary>
    public async Task<CustomerOrder?> GetOwnOrderAsync(int customerId, int orderId, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking().Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId, ct);

    /// <summary>
    /// Customer-initiated cancellation. Uses the STRICTER C# rule
    /// (PortalRules.CustomerCanCancel: Pending/Confirmed only), unlike OrderDesk's
    /// SQL rule which also allows 'Allocated'. Enforces record ownership.
    /// </summary>
    public async Task<ServiceResult> RequestCancellationAsync(int customerId, int orderId, string? reason, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId, ct);
        if (order is null) return ServiceResult.Fail("Order not found.");
        if (order.OrderStatus == "Cancelled") return ServiceResult.Fail("Order is already cancelled.");
        if (!PortalRules.CustomerCanCancel(order.OrderStatus))
            return ServiceResult.Fail($"Orders in '{order.OrderStatus}' status can no longer be cancelled online. Please contact support.");

        order.OrderStatus = "Cancelled";
        order.CancelReason = reason;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
