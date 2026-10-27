using CustomerPortal.Domain;
using CustomerPortal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Application;

/// <summary>Support tickets with validated status transitions (ownership enforced).</summary>
public class SupportTicketService
{
    private readonly PortalDbContext _db;
    public SupportTicketService(PortalDbContext db) => _db = db;

    public async Task<List<SupportTicket>> GetOwnTicketsAsync(int customerAccountId, CancellationToken ct = default)
        => await _db.SupportTickets.AsNoTracking().Where(t => t.CustomerAccountId == customerAccountId)
            .OrderByDescending(t => t.UpdatedUtc).ToListAsync(ct);

    public async Task<SupportTicket?> GetOwnTicketAsync(int customerAccountId, int ticketId, CancellationToken ct = default)
        => await _db.SupportTickets.AsNoTracking().Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId && t.CustomerAccountId == customerAccountId, ct);

    public async Task<ServiceResult<int>> CreateAsync(int customerAccountId, string subject, string body, int? relatedOrderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject)) return ServiceResult<int>.Fail("Subject is required.");
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            CustomerAccountId = customerAccountId,
            Subject = subject.Trim(),
            TicketStatus = "Open",
            RelatedOrderId = relatedOrderId,
            CreatedUtc = now,
            UpdatedUtc = now
        };
        ticket.Messages.Add(new SupportTicketMessage { Author = "Customer", Body = body, CreatedUtc = now });
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(ticket.SupportTicketId);
    }

    public async Task<ServiceResult> AddMessageAsync(int customerAccountId, int ticketId, string body, CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.SupportTicketId == ticketId && t.CustomerAccountId == customerAccountId, ct);
        if (ticket is null) return ServiceResult.Fail("Ticket not found.");
        if (ticket.TicketStatus == "Closed") return ServiceResult.Fail("Ticket is closed.");
        _db.SupportTicketMessages.Add(new SupportTicketMessage { SupportTicketId = ticketId, Author = "Customer", Body = body, CreatedUtc = DateTime.UtcNow });
        ticket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ChangeStatusAsync(int customerAccountId, int ticketId, string newStatus, CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.SupportTicketId == ticketId && t.CustomerAccountId == customerAccountId, ct);
        if (ticket is null) return ServiceResult.Fail("Ticket not found.");
        if (!PortalRules.IsValidTicketTransition(ticket.TicketStatus, newStatus))
            return ServiceResult.Fail($"Cannot move ticket from '{ticket.TicketStatus}' to '{newStatus}'.");
        ticket.TicketStatus = newStatus;
        ticket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
