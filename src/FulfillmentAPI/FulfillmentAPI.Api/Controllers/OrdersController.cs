using FulfillmentAPI.Application;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentAPI.Api.Controllers;

/// <summary>Pending fulfillment orders and order lookup (from both OrderDesk and CustomerPortal).</summary>
[ApiController]
[Route("api/fulfillment/orders")]
[Produces("application/json")]
public class OrdersController : FulfillmentControllerBase
{
    private readonly FulfillmentQueryService _query;
    public OrdersController(FulfillmentQueryService query) => _query = query;

    /// <summary>Retrieve orders awaiting fulfillment (Confirmed or Allocated).</summary>
    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _query.GetPendingOrdersAsync(ct));

    /// <summary>Retrieve a single order with its lines.</summary>
    [HttpGet("{orderId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int orderId, CancellationToken ct)
    {
        var order = await _query.GetOrderAsync(orderId, ct);
        return order is null ? NotFound(new { error = "Order not found." }) : Ok(order);
    }
}
