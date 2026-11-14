namespace MiniErp.Platform.Database;

/// <summary>
/// Marks an entity that belongs to one tenant (ADR-0003). For every such entity,
/// <see cref="ModuleDbContext"/> adds a query filter on the current tenant, and
/// <see cref="TenantSaveChangesInterceptor"/> stamps and guards <see cref="TenantId"/> on save.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
