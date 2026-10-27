using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Shopping-cart management + coupon application. Carts may contain products that
/// have become unavailable (allowed); availability is only enforced at checkout.
/// </summary>
public class CartService
{
    private readonly PortalDbContext _db;
    public CartService(PortalDbContext db) => _db = db;

    public async Task<ShoppingCart> GetOrCreateActiveCartAsync(int customerAccountId, CancellationToken ct = default)
    {
        var cart = await _db.Carts.Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == customerAccountId && c.CartStatus == "Active", ct);
        if (cart is null)
        {
            cart = new ShoppingCart { CustomerAccountId = customerAccountId, CartStatus = "Active", CreatedUtc = DateTime.UtcNow };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        return cart;
    }

    public async Task<ServiceResult> AddItemAsync(int customerAccountId, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return ServiceResult.Fail("Quantity must be positive.");
        if (!await _db.Products.AnyAsync(p => p.ProductId == productId, ct))
            return ServiceResult.Fail("Product not found.");

        var cart = await GetOrCreateActiveCartAsync(customerAccountId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            cart.Items.Add(new ShoppingCartItem { ShoppingCartId = cart.ShoppingCartId, ProductId = productId, Quantity = quantity, AddedUtc = DateTime.UtcNow });
        else
            item.Quantity += quantity;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateQuantityAsync(int customerAccountId, int productId, int quantity, CancellationToken ct = default)
    {
        var cart = await GetOrCreateActiveCartAsync(customerAccountId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return ServiceResult.Fail("Item not in cart.");
        if (quantity <= 0) { _db.CartItems.Remove(item); }
        else item.Quantity = quantity;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveItemAsync(int customerAccountId, int productId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateActiveCartAsync(customerAccountId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return ServiceResult.Fail("Item not in cart.");
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public static decimal CartSubTotal(ShoppingCart cart)
        => cart.Items.Sum(i => (i.Product?.UnitPrice ?? 0) * i.Quantity);

    /// <summary>
    /// Validates and applies a coupon code to the active cart (SCENARIO: coupon
    /// eligibility depends on type, customer status, cart total). Stores only the
    /// code on the cart; the discount is computed at checkout.
    /// </summary>
    public async Task<ServiceResult> ApplyCouponAsync(int customerAccountId, string customerStatus, string code, CancellationToken ct = default)
    {
        var cart = await GetOrCreateActiveCartAsync(customerAccountId, ct);
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct);
        if (coupon is null) return ServiceResult.Fail("Coupon code not found.");

        var total = CartSubTotal(cart);
        var redemptions = await _db.CouponRedemptions.CountAsync(r => r.CouponId == coupon.CouponId, ct);
        var (ok, reason) = PortalRules.EvaluateCoupon(coupon, total, customerStatus, redemptions, DateTime.UtcNow);
        if (!ok) return ServiceResult.Fail(reason ?? "Coupon not eligible.");

        cart.AppliedCouponCode = code;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveCouponAsync(int customerAccountId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateActiveCartAsync(customerAccountId, ct);
        cart.AppliedCouponCode = null;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
