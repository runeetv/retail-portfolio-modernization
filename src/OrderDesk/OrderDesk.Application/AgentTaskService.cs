using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>Assigned sales-agent tasks (OrderDesk-only capability).</summary>
public class AgentTaskService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public AgentTaskService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public async Task<List<SalesAgent>> ListAgentsAsync(CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.SalesAgents.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.FullName).ToListAsync(ct);
    }

    public async Task<List<SalesAgentTask>> ListTasksAsync(int salesAgentId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.SalesAgentTasks.AsNoTracking()
            .Where(t => t.SalesAgentId == salesAgentId && t.TaskStatus != "Cancelled")
            .OrderBy(t => t.TaskStatus).ThenBy(t => t.DueUtc)
            .ToListAsync(ct);
    }

    public async Task<OperationResult> UpdateStatusAsync(int taskId, string status, CancellationToken ct = default)
    {
        if (status is not ("Open" or "InProgress" or "Done" or "Cancelled"))
            return OperationResult.Fail("Invalid task status.");
        await using var db = _ctx();
        var task = await db.SalesAgentTasks.FirstOrDefaultAsync(t => t.SalesAgentTaskId == taskId, ct);
        if (task is null) return OperationResult.Fail("Task not found.");
        task.TaskStatus = status;
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<List<SalesCommission>> ListCommissionsAsync(int salesAgentId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.SalesCommissions.AsNoTracking()
            .Where(c => c.SalesAgentId == salesAgentId)
            .OrderByDescending(c => c.CreatedUtc).Take(100).ToListAsync(ct);
    }
}
