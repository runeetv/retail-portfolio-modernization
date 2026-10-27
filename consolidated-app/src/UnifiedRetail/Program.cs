using CustomerPortal.Infrastructure;
using FulfillmentAPI.Infrastructure;
using LegacyRetail.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrderDesk.Infrastructure;
using UnifiedRetail.Platform;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Persistence (DEC-PERSISTENCE, provisional PoC): we reuse the three proven
// per-app DbContexts over the ONE shared LegacyRetailDB connection (Stage 4
// Option B). This preserves each app's exact EF mappings and the stored-proc
// invocations verbatim, which is the lowest-risk path to behavioral parity.
// ---------------------------------------------------------------------------
var connectionString = DbConnectionConfig.Resolve(builder.Configuration.GetConnectionString("LegacyRetailDB"));

// OrderDesk services were written for a WPF app: they take Func<OrderDeskDbContext>
// (a factory that yields a fresh, short-lived context per operation). We register a
// DbContextFactory and expose the Func exactly as those services expect.
builder.Services.AddDbContextFactory<OrderDeskDbContext>(o => o.UseSqlServer(connectionString, s => s.EnableRetryOnFailure()));
builder.Services.AddScoped<Func<OrderDeskDbContext>>(sp =>
{
    var factory = sp.GetRequiredService<IDbContextFactory<OrderDeskDbContext>>();
    return () => factory.CreateDbContext();
});

builder.Services.AddDbContext<PortalDbContext>(o => o.UseSqlServer(connectionString, s => s.EnableRetryOnFailure()));
// FulfillmentDbContext: the fulfillment services open the EF connection manually and run raw-ADO
// readers for the stored procs (allocate / receive / activity-summary). Combining that manual
// connection handling with EF's retrying execution strategy can wedge the shared connection in a
// long-lived in-process host. A per-request command timeout keeps the cross-domain report snappy.
builder.Services.AddDbContext<FulfillmentDbContext>(o =>
    o.UseSqlServer(connectionString, s => s.CommandTimeout(60)));

// ---- Application services (reused verbatim from the three source apps) ----
// OrderDesk (staff)
builder.Services.AddScoped<OrderDesk.Application.CustomerService>();
builder.Services.AddScoped<OrderDesk.Application.CatalogService>();
builder.Services.AddScoped<OrderDesk.Application.QuoteService>();
builder.Services.AddScoped<OrderDesk.Application.OrderService>();
builder.Services.AddScoped<OrderDesk.Application.ReturnReviewService>();
builder.Services.AddScoped<OrderDesk.Application.AgentTaskService>();

// CustomerPortal (customer)
builder.Services.AddScoped<CustomerPortal.Application.AuthService>();
builder.Services.AddScoped<CustomerPortal.Application.ProfileService>();
builder.Services.AddScoped<CustomerPortal.Application.CatalogBrowseService>();
builder.Services.AddScoped<CustomerPortal.Application.CartService>();
builder.Services.AddScoped<CustomerPortal.Application.CheckoutService>();
builder.Services.AddScoped<CustomerPortal.Application.OrderHistoryService>();
builder.Services.AddScoped<CustomerPortal.Application.ReturnService>();
builder.Services.AddScoped<CustomerPortal.Application.TrackingService>();
builder.Services.AddScoped<CustomerPortal.Application.SupportTicketService>();
builder.Services.AddScoped<CustomerPortal.Application.WishlistService>();

// FulfillmentAPI (warehouse) — shared by the compatibility API and the warehouse UI
builder.Services.AddScoped<FulfillmentAPI.Application.FulfillmentQueryService>();
builder.Services.AddScoped<FulfillmentAPI.Application.AllocationService>();
builder.Services.AddScoped<FulfillmentAPI.Application.PickingService>();
builder.Services.AddScoped<FulfillmentAPI.Application.ShipmentService>();
builder.Services.AddScoped<FulfillmentAPI.Application.ReturnReceiptService>();

// ---- Authentication (cookie for interactive users) + authorization ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
    });

builder.Services.AddAuthorization(options =>
{
    // Role policies (DEC-ROLES, provisional PoC taxonomy from Stage 4 unified-web-requirements).
    options.AddPolicy("StaffOnly", p => p.RequireRole("SalesAgent", "SalesManager", "CustomerService"));
    options.AddPolicy("WarehouseOnly", p => p.RequireRole("WarehouseOperator", "Operations"));
    options.AddPolicy("CanOverrideCancellation", p => p.RequireRole("SalesManager"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UnifiedRetail — FulfillmentAPI (compat)", Version = "v1" });
    // Only document the preserved API surface; the MVC web controllers are not part of the API contract.
    c.DocInclusionPredicate((_, api) => (api.RelativePath ?? "").StartsWith("api/fulfillment", StringComparison.OrdinalIgnoreCase));
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Development API key. Default value: dev-fulfillment-key"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("ApiKey", doc), new List<string>() }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UnifiedRetail FulfillmentAPI v1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

// API-key auth (preserved for /api/fulfillment/*) runs before cookie auth so API
// clients are not redirected to a login page.
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "UnifiedRetail" }));

// Area routes (Staff, Warehouse) + default customer-facing route.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Attribute-routed API controllers (/api/fulfillment/*).
app.MapControllers();

app.Run();

// Expose Program for integration test host.
public partial class Program { }
