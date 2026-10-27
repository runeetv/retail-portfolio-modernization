using FulfillmentAPI.Api.Middleware;
using FulfillmentAPI.Api.Security;
using FulfillmentAPI.Application;
using FulfillmentAPI.Infrastructure;
using LegacyRetail.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var connectionString = DbConnectionConfig.Resolve(builder.Configuration.GetConnectionString("LegacyRetailDB"));

builder.Services.AddDbContext<FulfillmentDbContext>(opt =>
    opt.UseSqlServer(connectionString, o => o.EnableRetryOnFailure()));

builder.Services.AddScoped<FulfillmentQueryService>();
builder.Services.AddScoped<AllocationService>();
builder.Services.AddScoped<PickingService>();
builder.Services.AddScoped<ShipmentService>();
builder.Services.AddScoped<ReturnReceiptService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FulfillmentAPI",
        Version = "v1",
        Description = "Warehouse execution, inventory allocation, shipment processing, and returns receipt for LegacyRetailPortfolio."
    });

    // Document the API key auth scheme so Swagger UI can send X-Api-Key.
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Development API key. Default value: dev-fulfillment-key"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey", doc),
            new List<string>()
        }
    });

    var xml = Path.Combine(AppContext.BaseDirectory, "FulfillmentAPI.Api.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FulfillmentAPI v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ApiKeyMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "FulfillmentAPI" }));
app.MapControllers();

app.Run();

// Expose Program for integration test host.
public partial class Program { }
