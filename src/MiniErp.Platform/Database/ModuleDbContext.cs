using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MiniErp.Platform.Security;

namespace MiniErp.Platform.Database;

/// <summary>
/// Base class for every module's DbContext (ADR-0001, ADR-0002, ADR-0003):
/// the module's own PostgreSQL schema and migrations history table, and a tenant filter on
/// every <see cref="ITenantOwned"/> entity, so a query can only ever see the current tenant's rows.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The module's PostgreSQL schema; all of its tables live there.</summary>
    protected abstract string Schema { get; }

    /// <summary>
    /// Read by the query filters each time a query runs, so every request sees its own tenant.
    /// EF Core treats a DbContext member in a filter as a parameter, not as a constant baked
    /// into the cached model.
    /// </summary>
    private Guid CurrentTenantId => tenantContext.TenantId;

    /// <summary>Entity mappings of the module. Called before the tenant filters are added.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Each module keeps its migrations history in its own schema, so modules migrate independently.
        optionsBuilder.UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, Schema));
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureModel(modelBuilder);

        var applyFilter = typeof(ModuleDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tenantOwnedTypes = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(ITenantOwned).IsAssignableFrom(clrType))
            .ToList();

        foreach (var clrType in tenantOwnedTypes)
        {
            applyFilter.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
    }
}
