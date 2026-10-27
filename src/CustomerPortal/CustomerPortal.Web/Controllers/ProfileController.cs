using CustomerPortal.Application;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ProfileService _profile;
    public ProfileController(ProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var p = await _profile.GetAsync(User.CustomerId());
        if (p is null) return NotFound();
        return View(new ProfileViewModel
        {
            FirstName = p.FirstName,
            LastName = p.LastName,
            Email = p.Email,
            Phone = p.Phone,
            ShipAddressLine = p.ShipAddressLine,
            ShipCity = p.ShipCity,
            ShipRegion = p.ShipRegion,
            ShipPostalCode = p.ShipPostalCode,
            ShipCountry = p.ShipCountry,
            CustomerStatus = p.CustomerStatus
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        // Self-service edits only the allowed subset (phone + shipping address).
        var result = await _profile.UpdateSelfServiceAsync(
            User.CustomerId(), model.Phone, model.ShipAddressLine, model.ShipCity,
            model.ShipRegion, model.ShipPostalCode, model.ShipCountry);
        TempData["Message"] = result.Success ? "Profile updated." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
