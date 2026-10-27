using DbProvisioner;
using LegacyRetail.Common;
using Microsoft.Data.SqlClient;

/* ============================================================================
   LegacyRetailDB provisioner.
   Usage:
     dotnet run                 -> create DB, apply schema+procedures, seed data
     dotnet run -- --schema-only
     dotnet run -- --seed-only
     dotnet run -- --reset      -> DROP and recreate the database, then seed
   Idempotent & safe to rerun. Seeding uses a fixed RNG seed for determinism.
   ============================================================================ */

var options = ProvisionOptions.Parse(args);
var connStr = DbConnectionConfig.Resolve();
var masterConn = DbConnectionConfig.ResolveMaster();

// Locate the database/ directory (walk up from the executing assembly).
var scriptRoot = ScriptLocator.FindDatabaseRoot();
Console.WriteLine($"[provisioner] script root: {scriptRoot}");
Console.WriteLine($"[provisioner] target     : {new SqlConnectionStringBuilder(connStr).DataSource} / {DbConnectionConfig.DatabaseName}");

try
{
    if (options.Reset)
    {
        Console.WriteLine("[provisioner] RESET requested: dropping database...");
        DatabaseBootstrap.DropDatabase(masterConn);
    }

    DatabaseBootstrap.EnsureDatabase(masterConn);

    if (!options.SeedOnly)
    {
        Console.WriteLine("[provisioner] applying schema + procedures...");
        var sqlFiles = new[]
        {
            Path.Combine(scriptRoot, "schema", "01_schema.sql"),
            Path.Combine(scriptRoot, "schema", "02_orderdesk.sql"),
            Path.Combine(scriptRoot, "schema", "03_customerportal.sql"),
            Path.Combine(scriptRoot, "schema", "04_fulfillment.sql"),
            Path.Combine(scriptRoot, "procedures", "procedures.sql"),
        };
        foreach (var f in sqlFiles)
        {
            Console.WriteLine($"  - {Path.GetFileName(f)}");
            SqlScriptRunner.RunFile(connStr, f);
        }
    }

    if (!options.SchemaOnly)
    {
        Console.WriteLine("[provisioner] seeding deterministic sample data...");
        var seeder = new DataSeeder(connStr);
        seeder.Seed();
    }

    Console.WriteLine("[provisioner] DONE.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[provisioner] FAILED: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}
