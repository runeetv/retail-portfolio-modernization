using CustomerPortal.Application;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly CartService _cart;
    private readonly CheckoutService _checkout;
    public CheckoutController(CartService cart, CheckoutService checkout)
    {
        _cart = cart;
        _checkout = checkout;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cart.GetOrCreateActiveCartAsync(User.AccountId());
        // Generate a fresh idempotency key the view will post back, so an accidental
        // double submit maps to the same key and is deduplicated server-side.
        return View(new CheckoutViewModel
        {
            SubTotal = CartService.CartSubTotal(cart),
            AppliedCoupon = cart.AppliedCouponCode,
            ItemCount = cart.Items.Sum(i => i.Quantity),
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(string idempotencyKey)
    {
        var result = await _checkout.CheckoutAsync(
            User.AccountId(), User.CustomerId(), User.Status(), idempotencyKey);

        if (!result.Success)
        {
            var cart = await _cart.GetOrCreateActiveCartAsync(User.AccountId());
            return View("Index", new CheckoutViewModel
            {
                SubTotal = CartService.CartSubTotal(cart),
                AppliedCoupon = cart.AppliedCouponCode,
                ItemCount = cart.Items.Sum(i => i.Quantity),
                IdempotencyKey = idempotencyKey,
                Error = result.Error
            });
        }

        return RedirectToAction("Details", "Orders", new { id = result.Value });
    }
}
