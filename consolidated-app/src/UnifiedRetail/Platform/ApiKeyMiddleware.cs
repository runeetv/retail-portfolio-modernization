namespace UnifiedRetail.Platform;

/// <summary>
/// Backward-compatible API-key authentication for the preserved /api/fulfillment/* routes
/// (FA-RULE-023). Ported verbatim behavior from FulfillmentAPI.Api.Security.ApiKeyMiddleware:
/// every request UNDER /api/fulfillment must carry a valid X-Api-Key header. All other
/// (interactive/web) routes are unaffected and use cookie auth instead.
///
/// NOTE: This is the development-grade key scheme from the source app, preserved for the PoC
/// compatibility baseline. Production hardening is DEC-SEC (kept out of the compatibility baseline).
/// </summary>
public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _apiKey = config["ApiKey"] ?? "dev-fulfillment-key";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only the preserved fulfillment API surface is key-protected. Web UI + other
        // infrastructure endpoints (/, /health, /swagger, /openapi) are not.
        if (!path.StartsWith("/api/fulfillment", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !string.Equals(provided.ToString(), _apiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected request to {Path}: missing or invalid API key.", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Missing or invalid API key. Provide the X-Api-Key header.\"}");
            return;
        }

        await _next(context);
    }
}
