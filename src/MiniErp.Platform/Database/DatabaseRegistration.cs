using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MiniErp.Platform.Database;

public static class DatabaseRegistration
{
    /// <summary>Name of the connection string in configuration (<c>ConnectionStrings:MiniErp</c>).</summary>
    public const string ConnectionStringName = "MiniErp";

    /// <summary>Health check tag for checks that decide whether the API can take traffic.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers one shared <see cref="Npgsql.NpgsqlDataSource"/> (the connection pool) for the
    /// whole application, and a readiness health check that runs a query against it.
    /// Every module's DbContext will use this data source.
    /// </summary>
    public static IServiceCollection AddMiniErpDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing. Set ConnectionStrings:{ConnectionStringName} " +
                "in appsettings.Development.json or the ConnectionStrings__MiniErp environment variable.");
        }

        services.AddNpgsqlDataSource(connectionString);

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag]);

        return services;
    }
}
