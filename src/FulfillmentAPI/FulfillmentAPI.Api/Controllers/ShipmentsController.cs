using FulfillmentAPI.Api.Models;
using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

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

    /// <summary>Create a shipment package (requires all order items picked).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateShipmentRequest req, CancellationToken ct)
        => FromResult(await _shipments.CreateShipmentWithPackageAsync(req.OrderId, req.PackageCode, req.WeightKg, ct));

    /// <summary>Update shipment status (dispatch requires package + carrier reference).</summary>
    [HttpPut("{shipmentId:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int shipmentId, [FromBody] UpdateShipmentStatusRequest req, CancellationToken ct)
        => FromResult(await _shipments.UpdateStatusAsync(shipmentId, req.NewStatus, req.Carrier, req.TrackingNumber, ct));

    /// <summary>Record a carrier tracking event (idempotent on shipment/code/time).</summary>
    [HttpPost("{shipmentId:int}/events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordEvent(int shipmentId, [FromBody] CarrierEventRequest req, CancellationToken ct)
        => FromResult(await _shipments.RecordCarrierEventAsync(shipmentId, req.EventCode, req.Description, req.EventUtc, ct));

    /// <summary>Retrieve shipment tracking (packages + carrier events).</summary>
    [HttpGet("{shipmentId:int}/tracking")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tracking(int shipmentId, CancellationToken ct)
    {
        var s = await _query.GetShipmentTrackingAsync(shipmentId, ct);
        return s is null ? NotFound(new { error = "Shipment not found." }) : Ok(s);
    }

    /// <summary>List shipments for an order.</summary>
    [HttpGet("by-order/{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ByOrder(int orderId, CancellationToken ct)
        => Ok(await _query.GetShipmentsForOrderAsync(orderId, ct));
}
