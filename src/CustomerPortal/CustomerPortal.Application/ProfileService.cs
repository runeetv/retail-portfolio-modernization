using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Self-service profile (SCENARIO 1, CustomerPortal side). Customers may edit only
/// a SMALL subset of their profile (contact details + shipping), never status.
/// This is narrower than OrderDesk's staff editing.
/// </summary>
public class ProfileService
{
    private readonly PortalDbContext _db;
    public ProfileService(PortalDbContext db) => _db = db;

    public async Task<ShopperProfile?> GetAsync(int customerId, CancellationToken ct = default)
        => await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);

    /// <summary>Editable subset only: phone + shipping address. Name/email/status are NOT editable here.</summary>
    public async Task<ServiceResult> UpdateSelfServiceAsync(int customerId, string? phone, string? addr, string? city, string? region, string? postal, string? country, CancellationToken ct = default)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        if (profile is null) return ServiceResult.Fail("Profile not found.");

        profile.Phone = phone;
        profile.ShipAddressLine = addr;
        profile.ShipCity = city;
        profile.ShipRegion = region;
        profile.ShipPostalCode = postal;
        profile.ShipCountry = country;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<List<CustomerPreference>> GetPreferencesAsync(int customerAccountId, CancellationToken ct = default)
        => await _db.Preferences.AsNoTracking().Where(p => p.CustomerAccountId == customerAccountId).ToListAsync(ct);
}
