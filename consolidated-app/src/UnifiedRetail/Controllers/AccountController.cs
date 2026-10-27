using System.Security.Claims;
using CustomerPortal.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using UnifiedRetail.Platform;

namespace UnifiedRetail.Controllers;

/// <summary>
/// Customer local authentication (CP-CAP-001/002). Reuses CustomerPortal.AuthService
/// (PBKDF2 + lock check) and issues the same claim set the portal used so record-ownership
/// (CP-RULE-014) works identically. Issues the "Customer" role.
///
/// Staff / warehouse sign-in (M4/M5): the PoC uses the same login form with a role selector
/// for demonstration, since the source OrderDesk (WPF) and FulfillmentAPI (API key) had no
/// interactive web login. See implementation-progress.md DEC-ROLES.
/// </summary>
public class AccountController : Controller
{
    private readonly AuthService _auth;
    public AccountController(AuthService auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null, CancellationToken ct = default)
    {
        var result = await _auth.SignInAsync(username, password, ct);
        if (!result.Success || result.Value is null)
        {
            TempData["Err"] = result.Error ?? "Invalid username or password.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var account = result.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Role, "Customer"),
            new(CurrentUser.ClaimAccountId, account.CustomerAccountId.ToString()),
            new(CurrentUser.ClaimCustomerId, account.CustomerId.ToString()),
            new(CurrentUser.ClaimStatus, account.Profile?.CustomerStatus ?? "Standard"),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Catalog");
    }

    /// <summary>
    /// PoC demo sign-in for staff / warehouse roles (no interactive login existed in the source
    /// WPF / API apps). Grants the chosen role so the Staff/Warehouse areas can be exercised.
    /// This does NOT weaken customer auth; it is an explicit PoC affordance (DEC-ROLES / DEC-SEC).
    /// </summary>
    [HttpGet]
    public IActionResult Staff() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Staff(string displayName, string role, int agentId = 0)
    {
        var allowed = new[] { "SalesAgent", "SalesManager", "CustomerService", "WarehouseOperator", "Operations" };
        if (!allowed.Contains(role)) { TempData["Err"] = "Unknown role."; return View(); }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(displayName) ? role : displayName),
            new(ClaimTypes.Role, role),
            new(CurrentUser.ClaimAgentId, agentId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (role is "WarehouseOperator" or "Operations")
            return RedirectToAction("Index", "Home", new { area = "Warehouse" });
        return RedirectToAction("Index", "Home", new { area = "Staff" });
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Denied() => View();
}
