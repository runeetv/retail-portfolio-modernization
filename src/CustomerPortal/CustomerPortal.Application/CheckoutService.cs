using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Cart checkout (SCENARIO 2 / WORKFLOW B). Creates a CustomerPortal-sourced order.
/// - Rechecks inventory availability (SCENARIO 4 recheck step).
/// - Applies coupon discount (computed here, recorded as CouponRedemption).
/// - IDEMPOTENT: the same idempotency key returns the existing order rather than
///   creating a duplicate (protected by Orders.IdempotencyKey filtered-unique index).
/// </summary>
public class CheckoutService
{
    private readonly PortalDbContext _db;
    public CheckoutService(PortalDbContext db) => _db = db;

    public async Task<ServiceResult<int>> CheckoutAsync(int customerAccountId, int customerId, string customerStatus, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return ServiceResult<int>.Fail("Idempotency key is required.");

        // Idempotency: if an order already exists for this key, return it.
        var existing = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) return ServiceResult<int>.Ok(existing.OrderId);

        var cart = await _db.Carts.Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == customerAccountId && c.CartStatus == "Active", ct);
        if (cart is null || cart.Items.Count == 0)
            return ServiceResult<int>.Fail("Cart is empty.");

        // Mandatory customer info guard (DUPLICATED rule with OrderDesk + SQL).
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Email) || string.IsNullOrWhiteSpace(profile.ShipAddressLine))
            return ServiceResult<int>.Fail("Your profile is missing an email or shipping address.");

        // Recheck inventory availability for every line (authoritative check at checkout).
        foreach (var item in cart.Items)
        {
            var stock = await _db.Stock.FirstOrDefaultAsync(s => s.ProductId == item.ProductId, ct);
            if (stock is null || (stock.QuantityOnHand - stock.QuantityAllocated) < item.Quantity)
                return ServiceResult<int>.Fail($"'{item.Product?.Name ?? ("#" + item.ProductId)}' is no longer available in the requested quantity.");
        }

        decimal sub = cart.Items.Sum(i => (i.Product?.UnitPrice ?? 0) * i.Quantity);

        // Coupon discount, if a valid coupon is applied.
        decimal couponDiscount = 0m;
        Coupon? appliedCoupon = null;
        if (!string.IsNullOrWhiteSpace(cart.AppliedCouponCode))
        {
            appliedCoupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == cart.AppliedCouponCode, ct);
            if (appliedCoupon is not null)
            {
                var redemptions = await _db.CouponRedemptions.CountAsync(r => r.CouponId == appliedCoupon.CouponId, ct);
                var (ok, _) = PortalRules.EvaluateCoupon(appliedCoupon, sub, customerStatus, redemptions, DateTime.UtcNow);
                if (ok) couponDiscount = PortalRules.CouponDiscount(appliedCoupon, sub);
                else appliedCoupon = null; // coupon became invalid; ignore silently
            }
        }

        var order = new CustomerOrder
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customerId,
            OrderStatus = "Confirmed",
            OrderSource = "CustomerPortal",
            SubTotal = sub,
            DiscountTotal = couponDiscount,
            GrandTotal = Math.Round(sub - couponDiscount, 2),
            IdempotencyKey = idempotencyKey,
            PlacedUtc = DateTime.UtcNow
        };
        foreach (var item in cart.Items)
        {
            order.Lines.Add(new CustomerOrderLine
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Product?.UnitPrice ?? 0,
                LineDiscount = 0
            });
        }
        _db.Orders.Add(order);

        cart.CartStatus = "CheckedOut";
        cart.CheckedOutUtc = DateTime.UtcNow;

        // Wrap the transactional unit in the retrying execution strategy so it is
        // compatible with EnableRetryOnFailure (required for user-initiated transactions).
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                await _db.SaveChangesAsync(ct);

                if (appliedCoupon is not null && couponDiscount > 0)
                {
                    _db.CouponRedemptions.Add(new CouponRedemption
                    {
                        CouponId = appliedCoupon.CouponId,
                        CustomerAccountId = customerAccountId,
                        OrderId = order.OrderId,
                        AmountDiscounted = couponDiscount,
                        RedeemedUtc = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
            });
            return ServiceResult<int>.Ok(order.OrderId);
        }
        catch (DbUpdateException)
        {
            // A concurrent checkout with the same idempotency key won the race.
            var winner = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
            if (winner is not null) return ServiceResult<int>.Ok(winner.OrderId);
            return ServiceResult<int>.Fail("Checkout failed due to a conflict. Please retry.");
        }
    }
}
