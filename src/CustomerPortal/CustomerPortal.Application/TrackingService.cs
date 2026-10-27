using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Customer-friendly shipment tracking (SCENARIO 6). The portal only READS shipment
/// + carrier-event data owned by FulfillmentAPI, and presents it in a shopper view.
/// Ownership enforced via the order's CustomerId.
/// </summary>
public class TrackingService
{
    private readonly PortalDbContext _db;
    public TrackingService(PortalDbContext db) => _db = db;

    public async Task<List<TrackedShipment>> GetOrderTrackingAsync(int customerId, int orderId, CancellationToken ct = default)
    {
        var owns = await _db.Orders.AnyAsync(o => o.OrderId == orderId && o.CustomerId == customerId, ct);
        if (!owns) return new List<TrackedShipment>();

        return await _db.Shipments.AsNoTracking()
            .Include(s => s.Events)
            .Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);
    }

    public static string FriendlyStatus(string shipmentStatus) => shipmentStatus switch
    {
        "Created" => "Preparing your order",
        "Packed" => "Packed and ready",
        "Dispatched" => "Shipped",
        "InTransit" => "On its way",
        "Delivered" => "Delivered",
        "Exception" => "Delivery issue - contact support",
        _ => shipmentStatus
    };
}
