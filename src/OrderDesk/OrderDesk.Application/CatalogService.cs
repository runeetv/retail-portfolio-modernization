using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>Product catalog browsing + inventory availability (SCENARIO 4, OrderDesk variant).</summary>
public class CatalogService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public CatalogService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public async Task<List<Product>> BrowseAsync(string? term = null, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var q = db.Products.AsNoTracking().Include(p => p.Category).Where(p => !p.IsDiscontinued);
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            q = q.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }
        return await q.OrderBy(p => p.Name).Take(300).ToListAsync(ct);
    }

    /// <summary>
    /// OrderDesk availability check: simple on-hand-minus-allocated test used BEFORE
    /// accepting an order. (CustomerPortal shows availability + rechecks at checkout;
    /// FulfillmentAPI performs the physical allocation. Intentionally different impls.)
    /// </summary>
    public async Task<bool> IsAvailableAsync(int productId, int quantity, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var inv = await db.Inventory.AsNoTracking().FirstOrDefaultAsync(i => i.ProductId == productId, ct);
        return inv is not null && (inv.QuantityOnHand - inv.QuantityAllocated) >= quantity;
    }

    public async Task<int> AvailableQuantityAsync(int productId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var inv = await db.Inventory.AsNoTracking().FirstOrDefaultAsync(i => i.ProductId == productId, ct);
        return inv is null ? 0 : inv.QuantityOnHand - inv.QuantityAllocated;
    }
}
