using System.Text.Json;

namespace FulfillmentAPI.Api.Middleware;

/// <summary>Centralized error handling: converts unhandled exceptions to RFC7807-style JSON.</summary>
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
