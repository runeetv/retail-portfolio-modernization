using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>
/// Staff customer management (SCENARIO 1): OrderDesk can edit general contact info.
/// This is broader than CustomerPortal (self-service subset) and FulfillmentAPI (read-only shipping).
/// </summary>
public class CustomerService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public CustomerService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public async Task<List<Customer>> SearchAsync(string? term, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var q = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            q = q.Where(c => c.FirstName.Contains(term) || c.LastName.Contains(term) || c.Email.Contains(term));
        }
        return await q.OrderBy(c => c.LastName).Take(200).ToListAsync(ct);
    }

    public async Task<Customer?> GetAsync(int customerId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
    }

    /// <summary>Staff may update full contact info (name/email/phone/full shipping address).</summary>
    public async Task<OperationResult> UpdateContactAsync(Customer updated, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(updated.Email))
            return OperationResult.Fail("Email is required.");

        await using var db = _ctx();
        var entity = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == updated.CustomerId, ct);
        if (entity is null) return OperationResult.Fail("Customer not found.");

        entity.FirstName = updated.FirstName;
        entity.LastName = updated.LastName;
        entity.Email = updated.Email;
        entity.Phone = updated.Phone;
        entity.ShipAddressLine = updated.ShipAddressLine;
        entity.ShipCity = updated.ShipCity;
        entity.ShipRegion = updated.ShipRegion;
        entity.ShipPostalCode = updated.ShipPostalCode;
        entity.ShipCountry = updated.ShipCountry;

        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }
}
