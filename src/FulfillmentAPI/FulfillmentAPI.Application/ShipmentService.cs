using FulfillmentAPI.Domain;
using FulfillmentAPI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Application;

/// <summary>
/// Shipment packaging, status transitions, and carrier events (SCENARIO 6 owner /
/// WORKFLOW E). Enforces: items must be picked before packaging; dispatch requires
/// package + carrier reference; carrier events are idempotent.
/// </summary>
public class ShipmentService
{
    private readonly FulfillmentDbContext _db;
    public ShipmentService(FulfillmentDbContext db) => _db = db;

    /// <summary>Creates a shipment + package once all items for the order are picked.</summary>
    public async Task<ApiResult<Shipment>> CreateShipmentWithPackageAsync(int orderId, string packageCode, decimal? weightKg, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return ApiResult<Shipment>.Fail("Order not found.", ApiErrorKind.NotFound);
        if (FulfillmentRules.IsBlockedForFulfillment(order.OrderStatus))
            return ApiResult<Shipment>.Fail("Order is cancelled; cannot create shipment.", ApiErrorKind.Conflict);

        var picks = await _db.PickLists.Include(p => p.Items).Where(p => p.OrderId == orderId).ToListAsync(ct);
        var allPicked = picks.Count > 0 && picks.All(p => p.Items.All(i => i.IsPicked));
        if (!FulfillmentRules.CanPackageShipment(allPicked))
            return ApiResult<Shipment>.Fail("All items must be picked before packaging.", ApiErrorKind.Conflict);

        if (await _db.ShipmentPackages.AnyAsync(sp => sp.PackageCode == packageCode, ct))
            return ApiResult<Shipment>.Fail("Package code already exists.", ApiErrorKind.Conflict);

        var shipment = new Shipment
        {
            OrderId = orderId,
            ShipmentStatus = "Packed",
            CreatedUtc = DateTime.UtcNow
        };
        shipment.Packages.Add(new ShipmentPackage { PackageCode = packageCode, WeightKg = weightKg });
        _db.Shipments.Add(shipment);
        order.OrderStatus = "Packed";
        await _db.SaveChangesAsync(ct);
        return ApiResult<Shipment>.Ok(shipment);
    }

    /// <summary>
    /// Updates shipment status with transition validation. Dispatch additionally
    /// requires a package AND carrier reference (carrier + tracking number).
    /// </summary>
    public async Task<ApiResult<Shipment>> UpdateStatusAsync(int shipmentId, string newStatus, string? carrier, string? trackingNumber, CancellationToken ct = default)
    {
        var shipment = await _db.Shipments.Include(s => s.Packages).FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);
        if (shipment is null) return ApiResult<Shipment>.Fail("Shipment not found.", ApiErrorKind.NotFound);

        if (!FulfillmentRules.IsValidShipmentTransition(shipment.ShipmentStatus, newStatus))
            return ApiResult<Shipment>.Fail($"Invalid transition {shipment.ShipmentStatus} -> {newStatus}.", ApiErrorKind.Conflict);

        if (newStatus == "Dispatched")
        {
            var carrierVal = carrier ?? shipment.Carrier;
            var trackingVal = trackingNumber ?? shipment.TrackingNumber;
            if (!FulfillmentRules.CanDispatch(shipment.Packages.Count > 0, carrierVal, trackingVal))
                return ApiResult<Shipment>.Fail("Dispatch requires a package and a carrier reference (carrier + tracking number).", ApiErrorKind.Conflict);
            shipment.Carrier = carrierVal;
            shipment.TrackingNumber = trackingVal;
            shipment.DispatchedUtc = DateTime.UtcNow;
        }

        shipment.ShipmentStatus = newStatus;

        // Roll the order status forward to match major shipment milestones.
        var order = await _db.Orders.FirstAsync(o => o.OrderId == shipment.OrderId, ct);
        order.OrderStatus = newStatus switch
        {
            "Dispatched" => "Shipped",
            "Delivered" => "Delivered",
            _ => order.OrderStatus
        };

        await _db.SaveChangesAsync(ct);
        return ApiResult<Shipment>.Ok(shipment);
    }

    /// <summary>
    /// Records a carrier event. IDEMPOTENT on (ShipmentId, EventCode, EventUtc): a
    /// repeated event returns the existing row rather than inserting a duplicate.
    /// </summary>
    public async Task<ApiResult<CarrierEvent>> RecordCarrierEventAsync(int shipmentId, string eventCode, string? description, DateTime eventUtc, CancellationToken ct = default)
    {
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);
        if (shipment is null) return ApiResult<CarrierEvent>.Fail("Shipment not found.", ApiErrorKind.NotFound);

        var existing = await _db.CarrierEvents.FirstOrDefaultAsync(
            e => e.ShipmentId == shipmentId && e.EventCode == eventCode && e.EventUtc == eventUtc, ct);
        if (existing is not null) return ApiResult<CarrierEvent>.Ok(existing); // idempotent

        var evt = new CarrierEvent
        {
            ShipmentId = shipmentId,
            EventCode = eventCode,
            Description = description,
            EventUtc = eventUtc,
            RecordedUtc = DateTime.UtcNow
        };
        _db.CarrierEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
        return ApiResult<CarrierEvent>.Ok(evt);
    }
}
