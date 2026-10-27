using LegacyRetail.Common;
using Microsoft.Data.SqlClient;

namespace DbProvisioner;

public static class DatabaseBootstrap
{
    public static void EnsureDatabase(string masterConnectionString)
    {
        using var conn = new SqlConnection(masterConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            $"IF DB_ID(N'{DbConnectionConfig.DatabaseName}') IS NULL CREATE DATABASE [{DbConnectionConfig.DatabaseName}];",
            conn);
        cmd.ExecuteNonQuery();
    }

    public static void DropDatabase(string masterConnectionString)
    {
        using var conn = new SqlConnection(masterConnectionString);
        conn.Open();
        using var cmd = new SqlCommand($@"
IF DB_ID(N'{DbConnectionConfig.DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DbConnectionConfig.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DbConnectionConfig.DatabaseName}];
END", conn);
        cmd.ExecuteNonQuery();
    }
}
