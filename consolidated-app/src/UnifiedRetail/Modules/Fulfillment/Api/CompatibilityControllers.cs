using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedRetail.Modules.Fulfillment.Api;

// ---------------------------------------------------------------------------
// Backward-compatible /api/fulfillment/* controllers. Routes, verbs, request/response
// shapes, status codes, and error bodies are preserved from the original FulfillmentAPI
// (api-compatibility-inventory.yaml). Each delegates to the SAME application services the
// warehouse web UI uses — one business core, two surfaces. API-key auth is applied by
// UnifiedRetail.Platform.ApiKeyMiddleware for the /api/fulfillment path prefix.
// ---------------------------------------------------------------------------

/// <summary>Pending fulfillment orders and order lookup (OrderDesk + CustomerPortal origins).</summary>
[ApiController]
[Route("api/fulfillment/orders")]
[Produces("application/json")]
public class OrdersController : FulfillmentControllerBase
{
    private readonly FulfillmentQueryService _query;
    public OrdersController(FulfillmentQueryService query) => _query = query;

    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _query.GetPendingOrdersAsync(ct));

    [HttpGet("{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int orderId, CancellationToken ct)
    {
        var o = await _query.GetOrderAsync(orderId, ct);
        return o is null ? NotFound(new { error = "Order not found." }) : Ok(o);
    }
}

/// <summary>Product availability and inventory allocation (via stored procedure).</summary>
[ApiController]
[Route("api/fulfillment/inventory")]
[Produces("application/json")]
public class InventoryController : FulfillmentControllerBase
{
    private readonly FulfillmentQueryService _query;
    private readonly AllocationService _allocation;
    public InventoryController(FulfillmentQueryService query, AllocationService allocation)
    {
        _query = query;
        _allocation = allocation;
    }

    [HttpGet("availability/{productId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Availability(int productId, CancellationToken ct)
    {
        var stock = await _query.GetStockAsync(productId, ct);
        return stock is null
            ? NotFound(new { error = "No inventory record for product." })
            : Ok(new { stock.ProductId, stock.QuantityOnHand, stock.QuantityAllocated, stock.Available });
    }

    [HttpPost("allocate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Allocate([FromBody] AllocateRequest req, CancellationToken ct)
        => FromResult(await _allocation.AllocateAsync(req.OrderId, req.WarehouseId, ct));

    [HttpGet("allocations/{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Allocations(int orderId, CancellationToken ct)
        => Ok(await _allocation.GetAllocationsAsync(orderId, ct));
}

/// <summary>Pick list creation, bin assignment, and item picking.</summary>
[ApiController]
[Route("api/fulfillment/picking")]
[Produces("application/json")]
public class PickingController : FulfillmentControllerBase
{
    private readonly PickingService _picking;
    public PickingController(PickingService picking) => _picking = picking;

    [HttpPost("picklists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreatePickListRequest req, CancellationToken ct)
        => FromResult(await _picking.CreatePickListAsync(req.OrderId, req.WarehouseId, ct));

    [HttpGet("picklists/{pickListId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int pickListId, CancellationToken ct)
    {
        var pl = await _picking.GetPickListAsync(pickListId, ct);
        return pl is null ? NotFound(new { error = "Pick list not found." }) : Ok(pl);
    }

    [HttpPost("items/{pickListItemId:int}/pick")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPicked(int pickListItemId, [FromBody] MarkPickedRequest req, CancellationToken ct)
        => FromResult(await _picking.MarkItemPickedAsync(pickListItemId, req.QuantityPicked, ct));
}

/// <summary>Shipment packaging, status updates, carrier events, and tracking.</summary>
[ApiController]
[Route("api/fulfillment/shipments")]
[Produces("application/json")]
public class ShipmentsController : FulfillmentControllerBase
{
    private readonly ShipmentService _shipments;
    private readonly FulfillmentQueryService _query;
    public ShipmentsController(ShipmentService shipments, FulfillmentQueryService query)
    {
        _shipments = shipments;
        _query = query;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateShipmentRequest req, CancellationToken ct)
        => FromResult(await _shipments.CreateShipmentWithPackageAsync(req.OrderId, req.PackageCode, req.WeightKg, ct));

    [HttpPut("{shipmentId:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int shipmentId, [FromBody] UpdateShipmentStatusRequest req, CancellationToken ct)
        => FromResult(await _shipments.UpdateStatusAsync(shipmentId, req.NewStatus, req.Carrier, req.TrackingNumber, ct));

    [HttpPost("{shipmentId:int}/events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordEvent(int shipmentId, [FromBody] CarrierEventRequest req, CancellationToken ct)
        => FromResult(await _shipments.RecordCarrierEventAsync(shipmentId, req.EventCode, req.Description, req.EventUtc, ct));

    [HttpGet("{shipmentId:int}/tracking")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tracking(int shipmentId, CancellationToken ct)
    {
        var s = await _query.GetShipmentTrackingAsync(shipmentId, ct);
        return s is null ? NotFound(new { error = "Shipment not found." }) : Ok(s);
    }

    [HttpGet("by-order/{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ByOrder(int orderId, CancellationToken ct)
        => Ok(await _query.GetShipmentsForOrderAsync(orderId, ct));
}

/// <summary>Warehouse receipt of returned merchandise and resulting inventory adjustments.</summary>
[ApiController]
[Route("api/fulfillment/returns")]
[Produces("application/json")]
public class ReturnsController : FulfillmentControllerBase
{
    private readonly ReturnReceiptService _receipts;
    public ReturnsController(ReturnReceiptService receipts) => _receipts = receipts;

    [HttpPost("receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receive([FromBody] ReceiveReturnRequest req, CancellationToken ct)
        => FromResult(await _receipts.ReceiveAsync(req.ReturnId, req.WarehouseId, ct));

    [HttpGet("receipts/{returnReceiptId:int}/adjustments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjustments(int returnReceiptId, CancellationToken ct)
        => Ok(await _receipts.GetAdjustmentsForReceiptAsync(returnReceiptId, ct));
}

/// <summary>Warehouse reference data and cross-domain activity summary.</summary>
[ApiController]
[Route("api/fulfillment/warehouses")]
[Produces("application/json")]
public class WarehousesController : FulfillmentControllerBase
{
    private readonly FulfillmentQueryService _query;
    public WarehousesController(FulfillmentQueryService query) => _query = query;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _query.GetWarehousesAsync(ct));

    [HttpGet("activity-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivitySummary([FromQuery] int? warehouseId, CancellationToken ct)
        => Ok(await _query.GetActivitySummaryAsync(warehouseId, ct));
}
