using CustomerPortal.Application;
using CustomerPortal.Domain;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly OrderHistoryService _history;
    private readonly ReturnService _returns;
    private readonly TrackingService _tracking;
    public OrdersController(OrderHistoryService history, ReturnService returns, TrackingService tracking)
    {
        _history = history;
        _returns = returns;
        _tracking = tracking;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _history.GetOwnOrdersAsync(User.CustomerId()));

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _history.GetOwnOrderAsync(User.CustomerId(), id);
        if (order is null) return NotFound();

        var vm = new OrderDetailViewModel
        {
            Order = order,
            CanCancel = PortalRules.CustomerCanCancel(order.OrderStatus),
            CanReturn = PortalRules.CanRequestReturn(order.OrderStatus, order.PlacedUtc, DateTime.UtcNow),
            Shipments = await _tracking.GetOrderTrackingAsync(User.CustomerId(), id)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var result = await _history.RequestCancellationAsync(User.CustomerId(), id, reason);
        TempData["Message"] = result.Success ? "Order cancelled." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(int id, string? reason)
    {
        var result = await _returns.RequestReturnAsync(User.CustomerId(), id, reason);
        TempData["Message"] = result.Success ? "Return requested. Our team will review it." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
