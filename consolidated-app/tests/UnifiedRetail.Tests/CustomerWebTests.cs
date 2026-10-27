using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UnifiedRetail.Tests;

/// <summary>
/// Customer web authentication + authorization + parity (M3). Uses the seeded account
/// user1 / Password123! against the real LegacyRetailDB. Verifies cookie login, that
/// customer-only pages require auth, and record-ownership behavior (CP-RULE-014).
/// </summary>
[Collection("api")]
public class CustomerWebTests
{
    private readonly ApiTestFixture _f;
    public CustomerWebTests(ApiTestFixture f) => _f = f;

    private static HttpClient Browser(ApiTestFixture f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> AntiForgeryTokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient c, string user, string pass)
    {
        var token = await AntiForgeryTokenAsync(c, "/Account/Login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = user,
            ["password"] = pass,
            ["__RequestVerificationToken"] = token,
        });
        return await c.PostAsync("/Account/Login", form);
    }

    [Fact]
    public async Task Anonymous_can_browse_catalog()
    {
        var c = Browser(_f);
        var r = await c.GetAsync("/Catalog/Index");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact] // customer-only area requires auth -> redirect to login
    public async Task Cart_requires_authentication()
    {
        var c = Browser(_f);
        var r = await c.GetAsync("/Cart/Index");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location?.OriginalString ?? "");
    }

    [Fact] // CP-CAP-001: valid seeded login succeeds and establishes a session
    public async Task Valid_login_then_cart_is_accessible()
    {
        var c = Browser(_f);
        var login = await LoginAsync(c, "user1", "Password123!");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); // redirect to catalog on success

        var cart = await c.GetAsync("/Cart/Index");
        Assert.Equal(HttpStatusCode.OK, cart.StatusCode); // now authorized
    }

    [Fact] // CP-RULE-001: bad password is rejected (stays on login, no redirect)
    public async Task Invalid_login_is_rejected()
    {
        var c = Browser(_f);
        var login = await LoginAsync(c, "user1", "wrong-password");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); // re-renders login view, not a redirect
    }

    [Fact] // CP-CAP-010 + CP-RULE-014: own orders list renders for an authenticated customer
    public async Task Authenticated_customer_sees_own_orders_page()
    {
        var c = Browser(_f);
        await LoginAsync(c, "user1", "Password123!");
        var r = await c.GetAsync("/Orders/Index");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact] // CP-RULE-014: a very large order id the customer does not own -> redirected away (not shown)
    public async Task Ownership_blocks_unowned_order_detail()
    {
        var c = Browser(_f);
        await LoginAsync(c, "user1", "Password123!");
        var r = await c.GetAsync("/Orders/Details/99999999");
        // Not found for this customer -> controller redirects back to the list.
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Orders", r.Headers.Location?.OriginalString ?? "");
    }
}
