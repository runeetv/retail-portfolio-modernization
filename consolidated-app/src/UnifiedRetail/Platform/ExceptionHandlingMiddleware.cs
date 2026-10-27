using System.Text.Json;

namespace UnifiedRetail.Platform;

/// <summary>
/// Centralized error handling for the preserved API surface (FA-RULE-024): converts unhandled
/// exceptions to RFC7807-style application/problem+json. Ported verbatim from
/// FulfillmentAPI.Api.Middleware.ExceptionHandlingMiddleware so 500 responses match the contract.
/// Scoped to API routes; web UI uses the MVC error page.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isApi = (context.Request.Path.Value ?? "").StartsWith("/api/fulfillment", StringComparison.OrdinalIgnoreCase);
        if (!isApi)
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var payload = new
            {
                type = "about:blank",
                title = "An unexpected error occurred.",
                status = 500,
                detail = ex.Message
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
