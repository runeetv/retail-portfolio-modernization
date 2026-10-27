using CustomerPortal.Application;
using CustomerPortal.Infrastructure;
using LegacyRetail.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = DbConnectionConfig.Resolve(builder.Configuration.GetConnectionString("LegacyRetailDB"));

builder.Services.AddDbContext<PortalDbContext>(opt =>
    opt.UseSqlServer(connectionString, o => o.EnableRetryOnFailure()));

// Application services (scoped alongside the DbContext).
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<CatalogBrowseService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<OrderHistoryService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<SupportTicketService>();
builder.Services.AddScoped<WishlistService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
    });
builder.Services.AddAuthorization();

// Antiforgery is on by default for MVC; views use the antiforgery token via form tag helpers.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Expose Program for integration test host.
public partial class Program { }
