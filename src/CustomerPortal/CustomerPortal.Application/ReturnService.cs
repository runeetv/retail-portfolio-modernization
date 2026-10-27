using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Customer-initiated return requests (SCENARIO 5 / WORKFLOW C start). The portal
/// only CREATES the request ('Requested'); OrderDesk reviews/approves and
/// FulfillmentAPI records physical receipt. Return window enforced here (C# rule).
/// </summary>
public class ReturnService
{
    private readonly PortalDbContext _db;
    public ReturnService(PortalDbContext db) => _db = db;

    public async Task<List<CustomerReturn>> GetOwnReturnsAsync(int customerId, CancellationToken ct = default)
        => await _db.Returns.AsNoTracking().Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RequestedUtc).ToListAsync(ct);

    public async Task<ServiceResult<int>> RequestReturnAsync(int customerId, int orderId, string? reason, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId, ct);
        if (order is null) return ServiceResult<int>.Fail("Order not found.");
        if (!PortalRules.CanRequestReturn(order.OrderStatus, order.PlacedUtc, DateTime.UtcNow))
            return ServiceResult<int>.Fail($"This order is not eligible for return (must be Delivered within {PortalRules.ReturnWindowDays} days).");

        if (await _db.Returns.AnyAsync(r => r.OrderId == orderId && r.ReturnStatus != "Rejected", ct))
            return ServiceResult<int>.Fail("A return already exists for this order.");

        var ret = new CustomerReturn
        {
            ReturnNumber = $"RET-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            OrderId = orderId,
            CustomerId = customerId,
            ReturnStatus = "Requested",
            Reason = reason,
            RequestedUtc = DateTime.UtcNow
        };
        foreach (var line in order.Lines)
            ret.Lines.Add(new CustomerReturnLine { ProductId = line.ProductId, Quantity = line.Quantity });

        _db.Returns.Add(ret);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(ret.ReturnId);
    }
}
