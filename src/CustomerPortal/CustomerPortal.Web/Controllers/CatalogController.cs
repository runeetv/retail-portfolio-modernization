using CustomerPortal.Application;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

public class CatalogController : Controller
{
    private readonly CatalogBrowseService _catalog;
    private readonly WishlistService _wishlist;
    public CatalogController(CatalogBrowseService catalog, WishlistService wishlist)
    {
        _catalog = catalog;
        _wishlist = wishlist;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? term, int? categoryId)
    {
        var products = await _catalog.SearchAsync(term, categoryId);
        var vm = new CatalogViewModel
        {
            Term = term,
            CategoryId = categoryId,
            Categories = await _catalog.GetCategoriesAsync(),
            Products = products
        };
        foreach (var p in products)
            vm.Availability[p.ProductId] = await _catalog.DisplayAvailabilityAsync(p.ProductId);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _catalog.GetAsync(id);
        if (product is null) return NotFound();
        ViewData["Available"] = await _catalog.DisplayAvailabilityAsync(id);
        return View(product);
    }
}
