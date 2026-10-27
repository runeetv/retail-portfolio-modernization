using OrderDesk.Application;
using OrderDesk.Infrastructure;
using Xunit;

namespace IntegrationTests;

[Collection("Database")]
public class OrderDeskWorkflowTests
{
    private readonly DbFixture _fx;
    public OrderDeskWorkflowTests(DbFixture fx) => _fx = fx;

    private Func<OrderDeskDbContext> Ctx => () => _fx.OrderDesk();

    [Fact]
    public async Task Discount_above_threshold_routes_to_pending_approval_then_approve_enables_conversion()
    {
        Assert.True(_fx.DatabaseAvailable, "LegacyRetailDB must be provisioned.");
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(withShipping: true);
        var agentId = td.SalesAgent();
        var productId = td.CreateProduct(price: 100m, stock: 50);

        var quotes = new QuoteService(Ctx);

        var created = await quotes.CreateQuoteAsync(customerId, agentId);
        Assert.True(created.Success);
        var qid = created.Value!.SalesQuoteId;

        Assert.True((await quotes.AddLineAsync(qid, productId, 3)).Success);

        // 25% > 15% threshold -> PendingApproval + a DiscountApproval row.
        Assert.True((await quotes.ApplyDiscountAsync(qid, 25m)).Success);
        var pending = await quotes.ListPendingApprovalsAsync();
        var mine = pending.First(p => p.SalesQuoteId == qid);

        // Cannot convert while pending approval.
        var premature = await quotes.ConvertToOrderAsync(qid);
        Assert.False(premature.Success);

        // Approve -> quote becomes Approved -> conversion succeeds.
        Assert.True((await quotes.DecideDiscountAsync(mine.DiscountApprovalId, approve: true, decidingAgentId: agentId, notes: "ok")).Success);
        var converted = await quotes.ConvertToOrderAsync(qid);
        Assert.True(converted.Success, converted.Error);
        Assert.True(converted.Value > 0);
    }

    [Fact]
    public async Task Quote_conversion_prevents_double_convert()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var agentId = td.SalesAgent();
        var productId = td.CreateProduct(100m, 50);

        var quotes = new QuoteService(Ctx);
        var qid = (await quotes.CreateQuoteAsync(customerId, agentId)).Value!.SalesQuoteId;
        await quotes.AddLineAsync(qid, productId, 1);
        await quotes.ApplyDiscountAsync(qid, 5m); // below threshold -> auto approved

        var first = await quotes.ConvertToOrderAsync(qid);
        Assert.True(first.Success, first.Error);

        var second = await quotes.ConvertToOrderAsync(qid);
        Assert.False(second.Success);  // double conversion prevented
    }

    [Fact]
    public async Task Convert_fails_when_customer_missing_mandatory_info()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(withShipping: false); // no shipping address
        var agentId = td.SalesAgent();
        var productId = td.CreateProduct(100m, 50);

        var quotes = new QuoteService(Ctx);
        var qid = (await quotes.CreateQuoteAsync(customerId, agentId)).Value!.SalesQuoteId;
        await quotes.AddLineAsync(qid, productId, 1);
        await quotes.ApplyDiscountAsync(qid, 5m);

        var result = await quotes.ConvertToOrderAsync(qid);
        Assert.False(result.Success);
        Assert.Contains("mandatory", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Staff_cancellation_eligibility_allows_allocated_via_sql_rule()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var orderId = td.CreateOrder(customerId, productId, "Allocated", "OrderDesk");

        var orders = new OrderService(Ctx);
        var (eligible, _) = await orders.CheckCancellationEligibilityAsync(orderId);
        Assert.True(eligible); // OrderDesk SQL rule permits Allocated
    }

    [Fact]
    public async Task Cancellation_override_requires_authorized_agent_and_writes_audit()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var orderId = td.CreateOrder(customerId, productId, "Shipped", "OrderDesk"); // not normally cancellable

        var unauthorizedAgent = td.SalesAgent(canOverride: false);
        var authorizedAgent = td.SalesAgent(canOverride: true);
        var orders = new OrderService(Ctx);

        // Without override -> fails (not eligible).
        var noOverride = await orders.CancelOrderAsync(orderId, authorizedAgent, "reason", forceOverride: false);
        Assert.False(noOverride.Success);

        // Override by unauthorized agent -> fails.
        var unauth = await orders.CancelOrderAsync(orderId, unauthorizedAgent, "reason", forceOverride: true, overrideJustification: "please");
        Assert.False(unauth.Success);

        // Override by authorized agent with justification -> succeeds + audit row.
        var ok = await orders.CancelOrderAsync(orderId, authorizedAgent, "reason", forceOverride: true, overrideJustification: "VIP escalation");
        Assert.True(ok.Success, ok.Error);
        Assert.Equal("Cancelled", td.GetOrderStatus(orderId));

        using var audit = new Microsoft.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM dbo.CancellationOverrides WHERE OrderId=@o", conn);
        audit.Parameters.AddWithValue("@o", orderId);
        Assert.True((int)audit.ExecuteScalar()! >= 1);
    }
}
