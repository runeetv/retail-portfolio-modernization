using LegacyRetail.Common;
using Microsoft.EntityFrameworkCore;

namespace OrderDesk.Infrastructure;

/// <summary>Creates configured OrderDeskDbContext instances for the WPF app/tests.</summary>
public static class OrderDeskContextFactory
{
    public static DbContextOptions<OrderDeskDbContext> BuildOptions(string? connectionString = null)
    {
        var cs = DbConnectionConfig.Resolve(connectionString);
        return new DbContextOptionsBuilder<OrderDeskDbContext>()
            .UseSqlServer(cs, o => o.EnableRetryOnFailure())
            .Options;
    }

    public static OrderDeskDbContext Create(string? connectionString = null)
        => new(BuildOptions(connectionString));
}
