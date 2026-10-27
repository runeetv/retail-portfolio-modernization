using CustomerPortal.Domain;
using Xunit;

namespace CustomerPortal.Tests;

public class PortalRulesTests
{
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Confirmed", true)]
    [InlineData("Allocated", false)]  // STRICTER than OrderDesk (which allows Allocated)
    [InlineData("Shipped", false)]
    [InlineData("Delivered", false)]
    public void CustomerCanCancel_is_stricter_than_staff(string status, bool expected)
        => Assert.Equal(expected, PortalRules.CustomerCanCancel(status));

    [Fact]
    public void Cancellation_divergence_from_OrderDesk_is_bounded_to_Allocated()
    {
        // The intentional bounded divergence: 'Allocated' is cancellable by staff but NOT by the customer.
        Assert.True(OrderDesk.Domain.OrderDeskRules.IsStaffCancellable("Allocated"));
        Assert.False(PortalRules.CustomerCanCancel("Allocated"));

        // For Pending/Confirmed both agree.
        foreach (var s in new[] { "Pending", "Confirmed" })
        {
            Assert.Equal(OrderDesk.Domain.OrderDeskRules.IsStaffCancellable(s), PortalRules.CustomerCanCancel(s));
        }
    }

    [Fact]
    public void CanRequestReturn_only_delivered_within_window()
    {
        var now = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(PortalRules.CanRequestReturn("Delivered", now.AddDays(-5), now));
        Assert.False(PortalRules.CanRequestReturn("Delivered", now.AddDays(-45), now)); // outside window
        Assert.False(PortalRules.CanRequestReturn("Shipped", now.AddDays(-1), now));    // not delivered
    }

    [Fact]
    public void EvaluateCoupon_percent_requires_active_min_total_and_status()
    {
        var now = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var vip = new Coupon { Code = "VIP20", CouponType = "Percent", Value = 20, MinCartTotal = 100, RequiredStatus = "VIP", IsActive = true, ExpiresUtc = now.AddDays(10) };

        // VIP customer, cart over minimum -> ok
        var (ok1, _) = PortalRules.EvaluateCoupon(vip, 150, "VIP", 0, now);
        Assert.True(ok1);

        // Non-VIP -> rejected
        var (ok2, reason2) = PortalRules.EvaluateCoupon(vip, 150, "Standard", 0, now);
        Assert.False(ok2);
        Assert.Contains("VIP", reason2);

        // Below minimum -> rejected
        var (ok3, _) = PortalRules.EvaluateCoupon(vip, 50, "VIP", 0, now);
        Assert.False(ok3);

        // Expired -> rejected
        var expired = new Coupon { Code = "OLD", CouponType = "Percent", Value = 10, IsActive = true, ExpiresUtc = now.AddDays(-1) };
        var (ok4, _) = PortalRules.EvaluateCoupon(expired, 100, "Standard", 0, now);
        Assert.False(ok4);

        // Redemption cap reached -> rejected
        var capped = new Coupon { Code = "CAP", CouponType = "Percent", Value = 10, IsActive = true, MaxRedemptions = 1 };
        var (ok5, _) = PortalRules.EvaluateCoupon(capped, 100, "Standard", 1, now);
        Assert.False(ok5);
    }

    [Theory]
    [InlineData("Percent", 10, 200, 20)]
    [InlineData("FixedAmount", 25, 200, 25)]
    [InlineData("FixedAmount", 500, 200, 200)]  // capped at cart total
    public void CouponDiscount_computes_amount(string type, decimal value, decimal cart, decimal expected)
    {
        var c = new Coupon { CouponType = type, Value = value };
        Assert.Equal(expected, PortalRules.CouponDiscount(c, cart));
    }

    [Theory]
    [InlineData("Open", "Pending", true)]
    [InlineData("Open", "Resolved", true)]
    [InlineData("Resolved", "Closed", true)]
    [InlineData("Closed", "Open", false)]
    [InlineData("Open", "Closed", false)]
    public void IsValidTicketTransition(string from, string to, bool expected)
        => Assert.Equal(expected, PortalRules.IsValidTicketTransition(from, to));
}
