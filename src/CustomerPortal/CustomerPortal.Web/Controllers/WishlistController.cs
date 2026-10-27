using CustomerPortal.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class WishlistController : Controller
{
    private readonly WishlistService _wishlist;
    public WishlistController(WishlistService wishlist) => _wishlist = wishlist;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _wishlist.GetOrCreateAsync(User.AccountId()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId)
    {
        await _wishlist.AddAsync(User.AccountId(), productId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId)
    {
        await _wishlist.RemoveAsync(User.AccountId(), productId);
        return RedirectToAction(nameof(Index));
    }
}
