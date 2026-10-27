namespace LegacyRetail.Common;

/// <summary>
/// Infrastructure-only helper (no business logic). Resolves the single shared
/// LegacyRetailDB connection string. Each application references this for the
/// physical connection only; entity models and business rules live per-app.
/// </summary>
public static class DbConnectionConfig
{
    public const string DatabaseName = "LegacyRetailDB";

    /// <summary>Default LocalDB connection used for development.</summary>
    public const string DefaultConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=LegacyRetailDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    /// <summary>Connection to the master DB (used only by the provisioner to create the database).</summary>
    public const string MasterConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>
    /// Resolves the connection string from the LEGACYRETAIL_CONNECTION environment
    /// variable, falling back to LocalDB. Allows container/SQL Server overrides.
    /// </summary>
    public static string Resolve(string? overrideValue = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue;
        var env = Environment.GetEnvironmentVariable("LEGACYRETAIL_CONNECTION");
        return string.IsNullOrWhiteSpace(env) ? DefaultConnectionString : env;
    }

    public static string ResolveMaster()
    {
        var env = Environment.GetEnvironmentVariable("LEGACYRETAIL_MASTER_CONNECTION");
        return string.IsNullOrWhiteSpace(env) ? MasterConnectionString : env;
    }
}
