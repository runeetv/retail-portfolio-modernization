using CustomerPortal.Infrastructure;
using FulfillmentAPI.Infrastructure;
using LegacyRetail.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderDesk.Infrastructure;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Shared fixture: resolves the live LegacyRetailDB connection and provides each
/// app's DbContext. Verifies the database is provisioned (skips gracefully if not).
/// These are true integration tests against persisted SQL Server data.
/// </summary>
public sealed class DbFixture
{
    public string ConnectionString { get; }
    public bool DatabaseAvailable { get; }

    public DbFixture()
    {
        ConnectionString = DbConnectionConfig.Resolve();
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Customers", conn);
            var count = (int)cmd.ExecuteScalar()!;
            DatabaseAvailable = count > 0;
        }
        catch
        {
            DatabaseAvailable = false;
        }
    }

    public OrderDeskDbContext OrderDesk() =>
        new(new DbContextOptionsBuilder<OrderDeskDbContext>().UseSqlServer(ConnectionString).Options);

    public PortalDbContext Portal() =>
        new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlServer(ConnectionString).Options);

    public FulfillmentDbContext Fulfillment() =>
        new(new DbContextOptionsBuilder<FulfillmentDbContext>().UseSqlServer(ConnectionString).Options);

    public SqlConnection OpenConnection()
    {
        var c = new SqlConnection(ConnectionString);
        c.Open();
        return c;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DbFixture> { }
