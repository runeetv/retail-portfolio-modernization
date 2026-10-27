namespace FulfillmentAPI.Api.Security;

/// <summary>
/// Simple development API-key authentication. Every request (except Swagger and
/// the health endpoint) must include header <c>X-Api-Key</c> matching the configured
/// key. Documented mechanism intended for the benchmark, not production security.
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
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
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
