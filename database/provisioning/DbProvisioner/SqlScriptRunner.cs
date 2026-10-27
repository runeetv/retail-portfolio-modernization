using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DbProvisioner;

public static class SqlScriptRunner
{
    /// <summary>Runs a .sql file, splitting on GO batch separators.</summary>
    public static void RunFile(string connectionString, string path)
    {
        var text = File.ReadAllText(path);
        RunScript(connectionString, text);
    }

    public static void RunScript(string connectionString, string script)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        foreach (var batch in SplitBatches(script))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
            cmd.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        // Split on lines consisting solely of GO (case-insensitive).
        var parts = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (trimmed.Length > 0) yield return trimmed;
        }
    }
}
