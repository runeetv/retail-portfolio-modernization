using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>Wishlist maintenance (CustomerPortal-only capability).</summary>
public class WishlistService
{
    private readonly PortalDbContext _db;
    public WishlistService(PortalDbContext db) => _db = db;

    public async Task<Wishlist> GetOrCreateAsync(int customerAccountId, CancellationToken ct = default)
    {
        var wl = await _db.Wishlists.Include(w => w.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(w => w.CustomerAccountId == customerAccountId, ct);
        if (wl is null)
        {
            wl = new Wishlist { CustomerAccountId = customerAccountId, Name = "My Wishlist", CreatedUtc = DateTime.UtcNow };
            _db.Wishlists.Add(wl);
            await _db.SaveChangesAsync(ct);
        }
        return wl;
    }

    public async Task<ServiceResult> AddAsync(int customerAccountId, int productId, CancellationToken ct = default)
    {
        if (!await _db.Products.AnyAsync(p => p.ProductId == productId, ct))
            return ServiceResult.Fail("Product not found.");
        var wl = await GetOrCreateAsync(customerAccountId, ct);
        if (wl.Items.Any(i => i.ProductId == productId)) return ServiceResult.Ok();
        wl.Items.Add(new WishlistItem { WishlistId = wl.WishlistId, ProductId = productId, AddedUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveAsync(int customerAccountId, int productId, CancellationToken ct = default)
    {
        var wl = await GetOrCreateAsync(customerAccountId, ct);
        var item = wl.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return ServiceResult.Fail("Item not on wishlist.");
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
