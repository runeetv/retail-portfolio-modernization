using CustomerPortal.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnifiedRetail.Platform;

namespace UnifiedRetail.Controllers;

/// <summary>Idempotent checkout (CP-CAP-009). Preserves idempotency key + inventory recheck + mandatory-info.</summary>
[Authorize(Roles = "Customer")]
public class CheckoutController : Controller
{
    private readonly CheckoutService _checkout;
    private readonly CartService _cart;
    public CheckoutController(CheckoutService checkout, CartService cart)
    {
        _checkout = checkout;
        _cart = cart;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = await _cart.GetOrCreateActiveCartAsync(User.AccountId(), ct);
        // Issue a fresh idempotency key for this checkout attempt (matches portal behavior).
        ViewBag.IdempotencyKey = Guid.NewGuid().ToString("N");
        ViewBag.SubTotal = CartService.CartSubTotal(cart);
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(string idempotencyKey, CancellationToken ct)
    {
        var r = await _checkout.CheckoutAsync(User.AccountId(), User.CustomerId(), User.Status(), idempotencyKey, ct);
        if (!r.Success)
        {
            TempData["Err"] = r.Error;
            return RedirectToAction(nameof(Index));
        }
        TempData["Ok"] = $"Order #{r.Value} placed.";
        return RedirectToAction("Details", "Orders", new { id = r.Value });
    }
}

/// <summary>Customer order history, self-cancel, return request, tracking (CP-CAP-010/011/012/013/014).</summary>
[Authorize(Roles = "Customer")]
public class OrdersController : Controller
{
    private readonly OrderHistoryService _orders;
    private readonly ReturnService _returns;
    private readonly TrackingService _tracking;
    public OrdersController(OrderHistoryService orders, ReturnService returns, TrackingService tracking)
    {
        _orders = orders;
        _returns = returns;
        _tracking = tracking;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _orders.GetOwnOrdersAsync(User.CustomerId(), ct));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _orders.GetOwnOrderAsync(User.CustomerId(), id, ct); // ownership enforced (CP-RULE-014)
        if (order is null) { TempData["Err"] = "Order not found."; return RedirectToAction(nameof(Index)); }
        ViewBag.Shipments = await _tracking.GetOrderTrackingAsync(User.CustomerId(), id, ct);
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        var r = await _orders.RequestCancellationAsync(User.CustomerId(), id, reason, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Order cancelled." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(int id, string? reason, CancellationToken ct)
    {
        var r = await _returns.RequestReturnAsync(User.CustomerId(), id, reason, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? $"Return #{r.Value} requested." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}

/// <summary>Customer returns list (CP-CAP-013).</summary>
[Authorize(Roles = "Customer")]
public class ReturnsController : Controller
{
    private readonly ReturnService _returns;
    public ReturnsController(ReturnService returns) => _returns = returns;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _returns.GetOwnReturnsAsync(User.CustomerId(), ct));
}

/// <summary>Self-service profile (CP-CAP-003). Phone + shipping only.</summary>
[Authorize(Roles = "Customer")]
public class ProfileController : Controller
{
    private readonly ProfileService _profiles;
    public ProfileController(ProfileService profiles) => _profiles = profiles;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var profile = await _profiles.GetAsync(User.CustomerId(), ct);
        if (profile is null) { TempData["Err"] = "Profile not found."; return RedirectToAction("Index", "Catalog"); }
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? phone, string? addr, string? city, string? region, string? postal, string? country, CancellationToken ct)
    {
        var r = await _profiles.UpdateSelfServiceAsync(User.CustomerId(), phone, addr, city, region, postal, country, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Profile updated." : r.Error;
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>Support tickets (CP-CAP-015/016/017).</summary>
[Authorize(Roles = "Customer")]
public class SupportController : Controller
{
    private readonly SupportTicketService _support;
    public SupportController(SupportTicketService support) => _support = support;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _support.GetOwnTicketsAsync(User.AccountId(), ct));

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string subject, string body, int? relatedOrderId, CancellationToken ct)
    {
        var r = await _support.CreateAsync(User.AccountId(), subject ?? "", body ?? "", relatedOrderId, ct);
        if (!r.Success) { TempData["Err"] = r.Error; return View(); }
        TempData["Ok"] = "Ticket created.";
        return RedirectToAction(nameof(Details), new { id = r.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var ticket = await _support.GetOwnTicketAsync(User.AccountId(), id, ct);
        if (ticket is null) { TempData["Err"] = "Ticket not found."; return RedirectToAction(nameof(Index)); }
        return View(ticket);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMessage(int id, string body, CancellationToken ct)
    {
        var r = await _support.AddMessageAsync(User.AccountId(), id, body ?? "", ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Reply added." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus, CancellationToken ct)
    {
        var r = await _support.ChangeStatusAsync(User.AccountId(), id, newStatus, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Status updated." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}

/// <summary>Wishlist (CP-CAP-018).</summary>
[Authorize(Roles = "Customer")]
public class WishlistController : Controller
{
    private readonly WishlistService _wishlist;
    public WishlistController(WishlistService wishlist) => _wishlist = wishlist;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _wishlist.GetOrCreateAsync(User.AccountId(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, CancellationToken ct)
    {
        var r = await _wishlist.AddAsync(User.AccountId(), productId, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Added to wishlist." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        var r = await _wishlist.RemoveAsync(User.AccountId(), productId, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Removed." : r.Error;
        return RedirectToAction(nameof(Index));
    }
}
