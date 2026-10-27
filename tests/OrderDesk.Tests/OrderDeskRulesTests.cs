using OrderDesk.Domain;
using Xunit;

namespace OrderDesk.Tests;

public class OrderDeskRulesTests
{
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Confirmed", true)]
    [InlineData("Allocated", true)]   // OrderDesk is more permissive than CustomerPortal
    [InlineData("Picking", false)]
    [InlineData("Shipped", false)]
    [InlineData("Delivered", false)]
    [InlineData("Cancelled", false)]
    public void IsStaffCancellable_matches_staff_rule(string status, bool expected)
        => Assert.Equal(expected, OrderDeskRules.IsStaffCancellable(status));

    [Theory]
    [InlineData(10, false)]
    [InlineData(15, false)]   // threshold is strictly greater-than
    [InlineData(15.01, true)]
    [InlineData(25, true)]
    public void RequiresDiscountApproval_above_threshold(decimal percent, bool expected)
        => Assert.Equal(expected, OrderDeskRules.RequiresDiscountApproval(percent));

    [Fact]
    public void CanConvertToOrder_only_when_approved_and_not_expired_and_not_converted()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var approved = new SalesQuote { QuoteStatus = "Approved", ExpiresUtc = now.AddDays(5) };
        Assert.True(OrderDeskRules.CanConvertToOrder(approved, now));

        var expired = new SalesQuote { QuoteStatus = "Approved", ExpiresUtc = now.AddDays(-1) };
        Assert.False(OrderDeskRules.CanConvertToOrder(expired, now));

        var draft = new SalesQuote { QuoteStatus = "Draft", ExpiresUtc = now.AddDays(5) };
        Assert.False(OrderDeskRules.CanConvertToOrder(draft, now));

        var alreadyConverted = new SalesQuote { QuoteStatus = "Approved", ExpiresUtc = now.AddDays(5), ConvertedOrderId = 99 };
        Assert.False(OrderDeskRules.CanConvertToOrder(alreadyConverted, now));
    }

    [Theory]
    [InlineData(1000, 0.03, 10, 30)]    // normal discount: full commission
    [InlineData(1000, 0.03, 25, 15)]    // aggressive discount (>=20): halved
    public void CalculateCommission_halves_for_aggressive_discount(decimal value, decimal rate, decimal disc, decimal expected)
        => Assert.Equal(expected, OrderDeskRules.CalculateCommission(value, rate, disc));

    [Fact]
    public void CustomerCanOrder_requires_email_and_shipping_address()
    {
        Assert.True(OrderDeskRules.CustomerCanOrder(new Customer { Email = "a@b.test", ShipAddressLine = "1 St" }));
        Assert.False(OrderDeskRules.CustomerCanOrder(new Customer { Email = "", ShipAddressLine = "1 St" }));
        Assert.False(OrderDeskRules.CustomerCanOrder(new Customer { Email = "a@b.test", ShipAddressLine = null }));
    }
}
