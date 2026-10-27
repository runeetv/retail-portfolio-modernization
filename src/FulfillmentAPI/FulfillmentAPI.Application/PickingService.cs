using FulfillmentAPI.Domain;
using FulfillmentAPI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Application;

/// <summary>
/// Pick-list creation, bin assignment, and item picking. Enforces sequencing:
/// picking cannot begin before allocation; order must not be cancelled.
/// </summary>
public class PickingService
{
    private readonly FulfillmentDbContext _db;
    public PickingService(FulfillmentDbContext db) => _db = db;

    public async Task<ApiResult<PickList>> CreatePickListAsync(int orderId, int warehouseId, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return ApiResult<PickList>.Fail("Order not found.", ApiErrorKind.NotFound);
        if (FulfillmentRules.IsBlockedForFulfillment(order.OrderStatus))
            return ApiResult<PickList>.Fail("Order is cancelled; cannot process fulfillment.", ApiErrorKind.Conflict);

        // Picking requires prior allocation (SCENARIO: picking cannot begin before allocation).
        var allocations = await _db.Allocations.Where(a => a.OrderId == orderId && a.AllocationStatus == "Allocated").ToListAsync(ct);
        if (allocations.Count == 0)
            return ApiResult<PickList>.Fail("Order has no inventory allocation yet. Allocate first.", ApiErrorKind.Conflict);

        if (await _db.PickLists.AnyAsync(p => p.OrderId == orderId, ct))
            return ApiResult<PickList>.Fail("A pick list already exists for this order.", ApiErrorKind.Conflict);

        var bins = await _db.WarehouseBins.Where(wb => wb.WarehouseId == warehouseId).ToListAsync(ct);
        var pick = new PickList { OrderId = orderId, WarehouseId = warehouseId, PickStatus = "Created", CreatedUtc = DateTime.UtcNow };
        int binIdx = 0;
        foreach (var alloc in allocations)
        {
            pick.Items.Add(new PickListItem
            {
                ProductId = alloc.ProductId,
                WarehouseBinId = bins.Count > 0 ? bins[binIdx++ % bins.Count].WarehouseBinId : (int?)null,
                QuantityToPick = alloc.QuantityAllocated,
                QuantityPicked = 0,
                IsPicked = false
            });
        }
        _db.PickLists.Add(pick);
        await _db.SaveChangesAsync(ct);
        return ApiResult<PickList>.Ok(pick);
    }

    public async Task<ApiResult<PickListItem>> MarkItemPickedAsync(int pickListItemId, int quantityPicked, CancellationToken ct = default)
    {
        var item = await _db.PickListItems.FirstOrDefaultAsync(i => i.PickListItemId == pickListItemId, ct);
        if (item is null) return ApiResult<PickListItem>.Fail("Pick list item not found.", ApiErrorKind.NotFound);
        if (quantityPicked < 0 || quantityPicked > item.QuantityToPick)
            return ApiResult<PickListItem>.Fail("Picked quantity out of range.");

        item.QuantityPicked = quantityPicked;
        item.IsPicked = quantityPicked >= item.QuantityToPick;

        // Roll up pick list + order status.
        var pick = await _db.PickLists.Include(p => p.Items).FirstAsync(p => p.PickListId == item.PickListId, ct);
        if (pick.Items.All(i => i.IsPicked))
            pick.PickStatus = "Completed";
        else if (pick.Items.Any(i => i.QuantityPicked > 0))
            pick.PickStatus = "InProgress";

        var order = await _db.Orders.FirstAsync(o => o.OrderId == pick.OrderId, ct);
        if (pick.PickStatus == "Completed") order.OrderStatus = "Packed" == order.OrderStatus ? order.OrderStatus : "Picking";
        if (order.OrderStatus is "Allocated" && pick.PickStatus is "InProgress" or "Completed") order.OrderStatus = "Picking";

        await _db.SaveChangesAsync(ct);
        return ApiResult<PickListItem>.Ok(item);
    }

    public async Task<PickList?> GetPickListAsync(int pickListId, CancellationToken ct = default)
        => await _db.PickLists.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.PickListId == pickListId, ct);

    public async Task<List<PickList>> GetPickListsForOrderAsync(int orderId, CancellationToken ct = default)
        => await _db.PickLists.AsNoTracking().Include(p => p.Items).Where(p => p.OrderId == orderId).ToListAsync(ct);
}
