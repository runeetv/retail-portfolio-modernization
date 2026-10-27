using FulfillmentAPI.Api.Models;
using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

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

    /// <summary>Check available stock for a product.</summary>
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

    /// <summary>Reserve/allocate inventory for an order at a warehouse. Enforced by usp_AllocateInventory.</summary>
    [HttpPost("allocate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Allocate([FromBody] AllocateRequest req, CancellationToken ct)
        => FromResult(await _allocation.AllocateAsync(req.OrderId, req.WarehouseId, ct));

    /// <summary>List inventory allocations for an order.</summary>
    [HttpGet("allocations/{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Allocations(int orderId, CancellationToken ct)
        => Ok(await _allocation.GetAllocationsAsync(orderId, ct));
}
