using CustomerPortal.Application;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly CartService _cart;
    public CartController(CartService cart) => _cart = cart;

    [HttpGet]
    public async Task<IActionResult> Index(string? message)
    {
        var cart = await _cart.GetOrCreateActiveCartAsync(User.AccountId());
        return View(new CartViewModel
        {
            Cart = cart,
            SubTotal = CartService.CartSubTotal(cart),
            AppliedCoupon = cart.AppliedCouponCode,
            Message = message
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        var result = await _cart.AddItemAsync(User.AccountId(), productId, quantity);
        return RedirectToAction(nameof(Index), new { message = result.Success ? "Added to cart." : result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        var result = await _cart.UpdateQuantityAsync(User.AccountId(), productId, quantity);
        return RedirectToAction(nameof(Index), new { message = result.Success ? "Updated." : result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId)
    {
        var result = await _cart.RemoveItemAsync(User.AccountId(), productId);
        return RedirectToAction(nameof(Index), new { message = result.Success ? "Removed." : result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string code)
    {
        var result = await _cart.ApplyCouponAsync(User.AccountId(), User.Status(), code);
        return RedirectToAction(nameof(Index), new { message = result.Success ? "Coupon applied." : result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoupon()
    {
        await _cart.RemoveCouponAsync(User.AccountId());
        return RedirectToAction(nameof(Index), new { message = "Coupon removed." });
    }
}
