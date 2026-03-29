using Npgsql;

namespace PickleBall.Infrastructure.Data;

/// <summary>
/// Normalizes PostgreSQL connection strings for cloud hosts (Supabase, pooler) and EF Core.
/// </summary>
public static class NpgsqlConnectionStringHelper
{
    /// <summary>
    /// Enables SSL when missing and disables server-side prepared statements for PgBouncer transaction pooling.
    /// </summary>
    public static string ForEfCore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var host = builder.Host ?? "";
        var isLocal = host.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                      || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
        var looksLikeCloud = host.Contains("supabase", StringComparison.OrdinalIgnoreCase)
                             || host.Contains("pooler", StringComparison.OrdinalIgnoreCase)
                             || host.Contains("amazonaws.com", StringComparison.OrdinalIgnoreCase);

        if (!isLocal && looksLikeCloud)
        {
            if (builder.SslMode is SslMode.Disable or SslMode.Prefer)
                builder.SslMode = SslMode.Require;

            // Transaction pooler (PgBouncer) + EF: avoid prepared statements that break across pooled connections
            builder.MaxAutoPrepare = 0;
        }

        return builder.ConnectionString;
    }
}
