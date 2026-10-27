using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using LegacyRetail.Common;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>
/// Development-friendly local authentication (PBKDF2 via LegacyRetail.Common).
/// Returns the account on success so the web layer can sign in a cookie principal.
/// </summary>
public class AuthService
{
    private readonly PortalDbContext _db;
    public AuthService(PortalDbContext db) => _db = db;

    public async Task<ServiceResult<CustomerAccount>> SignInAsync(string username, string password, CancellationToken ct = default)
    {
        var account = await _db.CustomerAccounts.Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Username == username, ct);
        if (account is null) return ServiceResult<CustomerAccount>.Fail("Invalid username or password.");
        if (account.IsLocked) return ServiceResult<CustomerAccount>.Fail("Account is locked.");
        if (!PasswordHasher.Verify(password, account.PasswordHash))
            return ServiceResult<CustomerAccount>.Fail("Invalid username or password.");

        account.LastLoginUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ServiceResult<CustomerAccount>.Ok(account);
    }

    public async Task<CustomerAccount?> GetAccountAsync(int customerAccountId, CancellationToken ct = default)
        => await _db.CustomerAccounts.AsNoTracking().Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.CustomerAccountId == customerAccountId, ct);
}
