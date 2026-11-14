using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniErp.Platform.Numbering;
using Npgsql;

namespace MiniErp.Platform.Database;

public static class DatabaseRegistration
{
    /// <summary>Name of the connection string in configuration (<c>ConnectionStrings:MiniErp</c>).</summary>
    public const string ConnectionStringName = "MiniErp";

    /// <summary>When true, pending migrations of every module are applied at start-up (development and tests).</summary>
    public const string MigrateOnStartupSetting = "Database:MigrateOnStartup";

    /// <summary>Health check tag for checks that decide whether the API can take traffic.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers one shared <see cref="NpgsqlDataSource"/> (the connection pool) for the whole
    /// application, the tenant guard for SaveChanges, a readiness health check, and Platform's
    /// own tables (number series).
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
        services.AddSingleton<ModuleDbContextRegistry>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag]);

        services.AddModuleDbContext<PlatformDbContext>();
        services.AddScoped<INumberSeriesService, NumberSeriesService>();

        return services;
    }

    /// <summary>
    /// Registers a module's DbContext on the shared connection pool, with snake_case names and
    /// the tenant guard, and records it so its migrations run at start-up.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<TenantSaveChangesInterceptor>()));

        ModuleDbContextRegistry.Register<TContext>(services);
        return services;
    }

    /// <summary>Applies pending migrations of every registered module DbContext, one after another.</summary>
    public static async Task MigrateModuleDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ModuleDbContextRegistry>();

        foreach (var contextType in registry.ContextTypes)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}

/// <summary>The module DbContext types registered in this application, in registration order.</summary>
internal sealed class ModuleDbContextRegistry(IEnumerable<ModuleDbContextRegistry.Entry> entries)
{
    internal sealed record Entry(Type ContextType);

    public IReadOnlyList<Type> ContextTypes { get; } = entries.Select(entry => entry.ContextType).ToList();

    public static void Register<TContext>(IServiceCollection services) where TContext : ModuleDbContext =>
        services.AddSingleton(new Entry(typeof(TContext)));
}
