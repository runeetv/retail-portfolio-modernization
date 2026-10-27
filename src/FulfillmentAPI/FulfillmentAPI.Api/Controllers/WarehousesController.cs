using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

/// <summary>Warehouse reference data and cross-domain activity summary.</summary>
[ApiController]
[Route("api/fulfillment/warehouses")]
[Produces("application/json")]
public class WarehousesController : FulfillmentControllerBase
{
    private readonly FulfillmentQueryService _query;
    public WarehousesController(FulfillmentQueryService query) => _query = query;

    /// <summary>List active warehouses.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _query.GetWarehousesAsync(ct));

    /// <summary>
    /// Warehouse activity summary (cross-domain report via usp_WarehouseActivitySummary).
    /// </summary>
    [HttpGet("activity-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivitySummary([FromQuery] int? warehouseId, CancellationToken ct)
        => Ok(await _query.GetActivitySummaryAsync(warehouseId, ct));
}
