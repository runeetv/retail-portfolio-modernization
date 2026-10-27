using CustomerPortal.Application;
using Microsoft.Data.SqlClient;
using Xunit;

namespace IntegrationTests;

[Collection("Database")]
public class CustomerPortalWorkflowTests
{
    private readonly DbFixture _fx;
    public CustomerPortalWorkflowTests(DbFixture fx) => _fx = fx;

    [Fact]
    public async Task Checkout_creates_order_and_is_idempotent()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var accountId = td.CreateCustomerAccount(customerId, out _);
        var productId = td.CreateProduct(40m, 100);

        using var db = _fx.Portal();
        var cart = new CartService(db);
        await cart.AddItemAsync(accountId, productId, 2);

        var checkout = new CheckoutService(db);
        var key = Guid.NewGuid().ToString("N");

        var first = await checkout.CheckoutAsync(accountId, customerId, "Standard", key);
        Assert.True(first.Success, first.Error);

        // Same key again -> same order id (idempotent), no duplicate.
        using var db2 = _fx.Portal();
        var second = await new CheckoutService(db2).CheckoutAsync(accountId, customerId, "Standard", key);
        Assert.True(second.Success);
        Assert.Equal(first.Value, second.Value);

        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Orders WHERE IdempotencyKey=@k", conn);
        cmd.Parameters.AddWithValue("@k", key);
        Assert.Equal(1, (int)cmd.ExecuteScalar()!);
    }

    [Fact]
    public async Task Checkout_rechecks_inventory_and_blocks_when_unavailable()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var accountId = td.CreateCustomerAccount(customerId, out _);
        var productId = td.CreateProduct(40m, 1); // only 1 in stock

        using var db = _fx.Portal();
        var cart = new CartService(db);
        await cart.AddItemAsync(accountId, productId, 5); // more than available

        var result = await new CheckoutService(db).CheckoutAsync(accountId, customerId, "Standard", Guid.NewGuid().ToString("N"));
        Assert.False(result.Success);
        Assert.Contains("available", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coupon_applied_reduces_order_total_and_records_redemption()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var accountId = td.CreateCustomerAccount(customerId, out _);
        var productId = td.CreateProduct(100m, 100);

        // Use the seeded WELCOME10 coupon (10% off, no min, no status requirement).
        using var db = _fx.Portal();
        var cart = new CartService(db);
        await cart.AddItemAsync(accountId, productId, 2); // subtotal 200
        var apply = await cart.ApplyCouponAsync(accountId, "Standard", "WELCOME10");
        Assert.True(apply.Success, apply.Error);

        var orderId = (await new CheckoutService(db).CheckoutAsync(accountId, customerId, "Standard", Guid.NewGuid().ToString("N"))).Value;

        using var cmd = new SqlCommand("SELECT DiscountTotal, GrandTotal FROM dbo.Orders WHERE OrderId=@o", conn);
        cmd.Parameters.AddWithValue("@o", orderId);
        using var r = cmd.ExecuteReader();
        r.Read();
        var discount = r.GetDecimal(0);
        var grand = r.GetDecimal(1);
        r.Close();
        Assert.Equal(20m, discount);  // 10% of 200
        Assert.Equal(180m, grand);

        using var cmd2 = new SqlCommand("SELECT COUNT(*) FROM dbo.CouponRedemptions WHERE OrderId=@o", conn);
        cmd2.Parameters.AddWithValue("@o", orderId);
        Assert.True((int)cmd2.ExecuteScalar()! >= 1);
    }

    [Fact]
    public async Task Customer_cancellation_is_stricter_blocks_allocated()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var allocatedOrder = td.CreateOrder(customerId, productId, "Allocated");
        var confirmedOrder = td.CreateOrder(customerId, productId, "Confirmed");

        using var db = _fx.Portal();
        var history = new OrderHistoryService(db);

        // Allocated -> customer cannot cancel (stricter than OrderDesk).
        var blocked = await history.RequestCancellationAsync(customerId, allocatedOrder, "changed mind");
        Assert.False(blocked.Success);

        // Confirmed -> customer can cancel.
        var ok = await history.RequestCancellationAsync(customerId, confirmedOrder, "changed mind");
        Assert.True(ok.Success, ok.Error);
        Assert.Equal("Cancelled", td.GetOrderStatus(confirmedOrder));
    }

    [Fact]
    public async Task Ownership_enforced_cannot_cancel_another_customers_order()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var owner = td.CreateCustomer(true);
        var intruder = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var orderId = td.CreateOrder(owner, productId, "Confirmed");

        using var db = _fx.Portal();
        var history = new OrderHistoryService(db);

        // Intruder attempts to cancel owner's order -> treated as not found (ownership filter).
        var result = await history.RequestCancellationAsync(intruder, orderId, "malice");
        Assert.False(result.Success);
        Assert.Equal("Confirmed", td.GetOrderStatus(orderId)); // unchanged
    }

    [Fact]
    public async Task Return_request_only_within_window_for_delivered()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var deliveredOrder = td.CreateOrder(customerId, productId, "Delivered");

        using var db = _fx.Portal();
        var returns = new ReturnService(db);
        var ok = await returns.RequestReturnAsync(customerId, deliveredOrder, "defective");
        Assert.True(ok.Success, ok.Error);

        // A non-delivered order cannot be returned.
        var confirmed = td.CreateOrder(customerId, productId, "Confirmed");
        var blocked = await returns.RequestReturnAsync(customerId, confirmed, "nope");
        Assert.False(blocked.Success);
    }
}
