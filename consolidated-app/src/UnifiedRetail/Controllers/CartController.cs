using CustomerPortal.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnifiedRetail.Platform;

namespace UnifiedRetail.Controllers;

/// <summary>Shopping cart + coupons (CP-CAP-006/007/008). Requires an authenticated customer.</summary>
[Authorize(Roles = "Customer")]
public class CartController : Controller
{
    private readonly CartService _cart;
    public CartController(CartService cart) => _cart = cart;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var cart = await _cart.GetOrCreateActiveCartAsync(User.AccountId(), ct);
        ViewBag.SubTotal = CartService.CartSubTotal(cart);
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity, CancellationToken ct)
    {
        var r = await _cart.AddItemAsync(User.AccountId(), productId, quantity <= 0 ? 1 : quantity, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Added to cart." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, int quantity, CancellationToken ct)
    {
        var r = await _cart.UpdateQuantityAsync(User.AccountId(), productId, quantity, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Cart updated." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        var r = await _cart.RemoveItemAsync(User.AccountId(), productId, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Item removed." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string code, CancellationToken ct)
    {
        var r = await _cart.ApplyCouponAsync(User.AccountId(), User.Status(), code ?? "", ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Coupon applied." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoupon(CancellationToken ct)
    {
        await _cart.RemoveCouponAsync(User.AccountId(), ct);
        TempData["Ok"] = "Coupon removed.";
        return RedirectToAction(nameof(Index));
    }
}
