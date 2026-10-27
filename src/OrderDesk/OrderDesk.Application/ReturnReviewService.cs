using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>
/// OrderDesk's role in the cross-app return workflow (SCENARIO 5 / WORKFLOW C):
/// review a customer-requested return and Approve/Reject it. Warehouse receipt
/// (FulfillmentAPI) happens separately only AFTER approval.
/// </summary>
public class ReturnReviewService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public ReturnReviewService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public async Task<List<ReturnRequest>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        await using var db = _ctx();
        IQueryable<ReturnRequest> q = db.Returns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.ReturnStatus == status);
        return await q.OrderByDescending(r => r.RequestedUtc).Take(200).ToListAsync(ct);
    }

    public async Task<OperationResult> ReviewAsync(int returnId, bool approve, int agentId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var ret = await db.Returns.FirstOrDefaultAsync(r => r.ReturnId == returnId, ct);
        if (ret is null) return OperationResult.Fail("Return not found.");
        if (ret.ReturnStatus != "Requested")
            return OperationResult.Fail($"Return is '{ret.ReturnStatus}'; only 'Requested' returns may be reviewed.");

        ret.ReturnStatus = approve ? "Approved" : "Rejected";
        ret.ReviewedByAgentId = agentId;
        ret.ReviewedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }
}
