using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Product browsing + availability DISPLAY (SCENARIO 4, CustomerPortal variant).
/// The portal shows availability to shoppers but the authoritative recheck happens
/// again at checkout (see CheckoutService). Different from OrderDesk's single check
/// and FulfillmentAPI's physical allocation.
/// </summary>
public class CatalogBrowseService
{
    private readonly PortalDbContext _db;
    public CatalogBrowseService(PortalDbContext db) => _db = db;

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
        => await _db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<List<CatalogProduct>> SearchAsync(string? term, int? categoryId, CancellationToken ct = default)
    {
        var q = _db.Products.AsNoTracking().Include(p => p.Category).Where(p => !p.IsDiscontinued);
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            q = q.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }
        if (categoryId is { } cid) q = q.Where(p => p.CategoryId == cid);
        return await q.OrderBy(p => p.Name).Take(200).ToListAsync(ct);
    }

    public async Task<CatalogProduct?> GetAsync(int productId, CancellationToken ct = default)
        => await _db.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductId == productId, ct);

    /// <summary>Availability shown in the UI. Note: carts may hold unavailable items.</summary>
    public async Task<int> DisplayAvailabilityAsync(int productId, CancellationToken ct = default)
    {
        var stock = await _db.Stock.AsNoTracking().FirstOrDefaultAsync(s => s.ProductId == productId, ct);
        return stock is null ? 0 : stock.QuantityOnHand - stock.QuantityAllocated;
    }
}
