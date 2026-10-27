using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedRetail.Areas.Warehouse.Controllers;

/// <summary>
/// Warehouse area (FulfillmentAPI functionality as a web UI). Uses the SAME application
/// services the backward-compatible /api/fulfillment/* endpoints use — one business core,
/// two surfaces. Requires a warehouse role (WarehouseOnly policy). Stored-proc-backed
/// operations (allocate, return receipt) are preserved verbatim.
/// </summary>
[Area("Warehouse")]
[Authorize(Policy = "WarehouseOnly")]
public class HomeController : Controller
{
    private readonly FulfillmentQueryService _query;
    public HomeController(FulfillmentQueryService query) => _query = query;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Pending = await _query.GetPendingOrdersAsync(ct);
        ViewBag.Warehouses = await _query.GetWarehousesAsync(ct);
        return View();
    }
}

[Area("Warehouse")]
[Authorize(Policy = "WarehouseOnly")]
public class FulfillController : Controller
{
    private readonly FulfillmentQueryService _query;
    private readonly AllocationService _allocation;
    private readonly PickingService _picking;
    private readonly ShipmentService _shipments;
    public FulfillController(FulfillmentQueryService query, AllocationService allocation, PickingService picking, ShipmentService shipments)
    {
        _query = query;
        _allocation = allocation;
        _picking = picking;
        _shipments = shipments;
    }

    [HttpGet]
    public async Task<IActionResult> Order(int id, CancellationToken ct)
    {
        var order = await _query.GetOrderAsync(id, ct);
        if (order is null) { TempData["Err"] = "Order not found."; return RedirectToAction("Index", "Home"); }
        ViewBag.Allocations = await _allocation.GetAllocationsAsync(id, ct);
        ViewBag.PickLists = await _picking.GetPickListsForOrderAsync(id, ct);
        ViewBag.Shipments = await _query.GetShipmentsForOrderAsync(id, ct);
        ViewBag.Warehouses = await _query.GetWarehousesAsync(ct);
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Allocate(int orderId, int warehouseId, CancellationToken ct)
    {
        var r = await _allocation.AllocateAsync(orderId, warehouseId, ct); // usp_AllocateInventory
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Inventory allocated." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePickList(int orderId, int warehouseId, CancellationToken ct)
    {
        var r = await _picking.CreatePickListAsync(orderId, warehouseId, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Pick list created." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pick(int orderId, int pickListItemId, int quantityPicked, CancellationToken ct)
    {
        var r = await _picking.MarkItemPickedAsync(pickListItemId, quantityPicked, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Item picked." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Package(int orderId, string packageCode, decimal? weightKg, CancellationToken ct)
    {
        var r = await _shipments.CreateShipmentWithPackageAsync(orderId, packageCode, weightKg, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Shipment packaged." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShipment(int orderId, int shipmentId, string newStatus, string? carrier, string? trackingNumber, CancellationToken ct)
    {
        var r = await _shipments.UpdateStatusAsync(shipmentId, newStatus, carrier, trackingNumber, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? $"Shipment -> {newStatus}." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarrierEvent(int orderId, int shipmentId, string eventCode, string? description, CancellationToken ct)
    {
        var r = await _shipments.RecordCarrierEventAsync(shipmentId, eventCode, description, DateTime.UtcNow, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Carrier event recorded." : r.Error;
        return RedirectToAction(nameof(Order), new { id = orderId });
    }
}

[Area("Warehouse")]
[Authorize(Policy = "WarehouseOnly")]
public class ReturnsController : Controller
{
    private readonly ReturnReceiptService _receipts;
    private readonly FulfillmentQueryService _query;
    public ReturnsController(ReturnReceiptService receipts, FulfillmentQueryService query)
    {
        _receipts = receipts;
        _query = query;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Warehouses = await _query.GetWarehousesAsync(ct);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int returnId, int warehouseId, CancellationToken ct)
    {
        var r = await _receipts.ReceiveAsync(returnId, warehouseId, ct); // usp_ReceiveReturnAndAdjust (Approved-only, idempotent)
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Return received + restocked." : r.Error;
        return RedirectToAction(nameof(Index));
    }
}

[Area("Warehouse")]
[Authorize(Policy = "WarehouseOnly")]
public class ReportsController : Controller
{
    private readonly FulfillmentQueryService _query;
    public ReportsController(FulfillmentQueryService query) => _query = query;

    [HttpGet]
    public async Task<IActionResult> Index(int? warehouseId, CancellationToken ct)
        => View(await _query.GetActivitySummaryAsync(warehouseId, ct)); // usp_WarehouseActivitySummary (cross-domain)
}
