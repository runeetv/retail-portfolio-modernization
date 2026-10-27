using CustomerPortal.Application;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedRetail.Controllers;

/// <summary>Customer catalog browse + product detail (CP-CAP-004/005). Anonymous allowed.</summary>
public class CatalogController : Controller
{
    private readonly CatalogBrowseService _catalog;
    public CatalogController(CatalogBrowseService catalog) => _catalog = catalog;

    [HttpGet]
    public async Task<IActionResult> Index(string? term, int? categoryId, CancellationToken ct)
    {
        ViewBag.Categories = await _catalog.GetCategoriesAsync(ct);
        ViewBag.Term = term;
        ViewBag.CategoryId = categoryId;
        var products = await _catalog.SearchAsync(term, categoryId, ct);
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await _catalog.GetAsync(id, ct);
        if (product is null) { TempData["Err"] = "Product not found."; return RedirectToAction(nameof(Index)); }
        ViewBag.Available = await _catalog.DisplayAvailabilityAsync(id, ct);
        return View(product);
    }
}
