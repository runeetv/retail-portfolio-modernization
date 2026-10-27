namespace DbProvisioner;

public static class ScriptLocator
{
    /// <summary>
    /// Walks up from the executing assembly / current directory to find the
    /// 'database' folder containing schema + procedures.
    /// </summary>
    public static string FindDatabaseRoot()
    {
        // Allow explicit override (useful for tests / CI).
        var env = Environment.GetEnvironmentVariable("LEGACYRETAIL_DB_SCRIPTS");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "database");
                if (Directory.Exists(Path.Combine(candidate, "schema")))
                    return candidate;
                dir = dir.Parent;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the 'database' folder containing SQL scripts. " +
            "Set LEGACYRETAIL_DB_SCRIPTS to its path.");
    }
}
