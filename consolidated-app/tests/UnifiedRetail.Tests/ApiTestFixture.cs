using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace UnifiedRetail.Tests;

/// <summary>
/// Shared WebApplicationFactory for the consolidated host. Tests run against the real
/// LegacyRetailDB (integration), matching the existing test suites' approach. A client
/// with the X-Api-Key header is provided for the preserved /api/fulfillment/* contract.
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>
{
    public const string ApiKey = "dev-fulfillment-key";

    private bool _warmed;

    public HttpClient CreateApiClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    /// <summary>
    /// Warms the host's DB connection + stored-proc plan cache once, so the first real assertion is
    /// not the cold-start outlier (JIT + first proc compile under the in-process host). Idempotent.
    /// </summary>
    public async Task WarmUpAsync()
    {
        if (_warmed) return;
        var c = CreateApiClient();
        try { await c.GetAsync("/api/fulfillment/warehouses"); } catch { /* best-effort */ }
        try { await c.GetAsync("/api/fulfillment/warehouses/activity-summary?warehouseId=1"); } catch { /* best-effort */ }
        _warmed = true;
    }

    public HttpClient CreateNoKeyClient() => CreateClient();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiTestFixture> { }
