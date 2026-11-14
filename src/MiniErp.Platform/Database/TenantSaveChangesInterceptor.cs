using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniErp.Platform.Security;

namespace MiniErp.Platform.Database;

/// <summary>A write would have touched another tenant's data. Always a bug, never a user error.</summary>
public sealed class CrossTenantWriteException(string message) : InvalidOperationException(message);

/// <summary>
/// Runs before every SaveChanges (ADR-0003): new tenant-owned rows get the current tenant,
/// and any insert, update or delete of another tenant's row is refused.
/// The query filter already hides other tenants' rows; this is the write-side guard for the
/// cases a filter cannot cover (a hand-built entity, IgnoreQueryFilters, a changed TenantId).
/// </summary>
internal sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        GuardTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        GuardTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void GuardTenant(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries<ITenantOwned>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var currentTenant = tenantContext.TenantId;

        foreach (var entry in entries)
        {
            var tenantId = entry.Property(entity => entity.TenantId);

            if (entry.State == EntityState.Added)
            {
                if (tenantId.CurrentValue == Guid.Empty)
                {
                    tenantId.CurrentValue = currentTenant;
                }
                else if (tenantId.CurrentValue != currentTenant)
                {
                    throw Refused("insert", entry.Metadata.ClrType, currentTenant);
                }
            }
            else if (tenantId.OriginalValue != currentTenant || tenantId.CurrentValue != currentTenant)
            {
                throw Refused(entry.State == EntityState.Deleted ? "delete" : "update", entry.Metadata.ClrType, currentTenant);
            }
        }
    }

    private static CrossTenantWriteException Refused(string operation, Type entityType, Guid currentTenant) =>
        new($"Refused to {operation} a {entityType.Name} that does not belong to the current tenant {currentTenant}.");
}
