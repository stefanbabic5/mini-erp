using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MiniErp.Platform.Database;

/// <summary>
/// Reports healthy when PostgreSQL answers a trivial query. Used by the readiness probe,
/// so the API reports "not ready" while the database is unreachable.
/// </summary>
internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", exception);
        }
    }
}
