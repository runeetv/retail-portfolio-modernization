using FulfillmentAPI.Api.Models;
using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

/// <summary>Pick list creation, bin assignment, and item picking.</summary>
[ApiController]
[Route("api/fulfillment/picking")]
[Produces("application/json")]
public class PickingController : FulfillmentControllerBase
{
    private readonly PickingService _picking;
    public PickingController(PickingService picking) => _picking = picking;

    /// <summary>Create a pick list for an allocated order (bins auto-assigned).</summary>
    [HttpPost("picklists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreatePickListRequest req, CancellationToken ct)
        => FromResult(await _picking.CreatePickListAsync(req.OrderId, req.WarehouseId, ct));

    /// <summary>Get a pick list with its items.</summary>
    [HttpGet("picklists/{pickListId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int pickListId, CancellationToken ct)
    {
        var pl = await _picking.GetPickListAsync(pickListId, ct);
        return pl is null ? NotFound(new { error = "Pick list not found." }) : Ok(pl);
    }

    /// <summary>Mark a pick-list item as (partially) picked.</summary>
    [HttpPost("items/{pickListItemId:int}/pick")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPicked(int pickListItemId, [FromBody] MarkPickedRequest req, CancellationToken ct)
        => FromResult(await _picking.MarkItemPickedAsync(pickListItemId, req.QuantityPicked, ct));
}
