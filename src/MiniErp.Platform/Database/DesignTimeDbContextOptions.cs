using Microsoft.EntityFrameworkCore;
using MiniErp.Platform.Security;

namespace MiniErp.Platform.Database;

/// <summary>
/// What <c>dotnet ef</c> needs to build a module DbContext without starting the API:
/// options for the same provider and naming convention, and a tenant context that is never
/// read (migrations do not run queries with tenant filters).
/// </summary>
public static class DesignTimeDbContextOptions
{
    // Only used to generate migrations; nothing connects with it.
    private const string DesignTimeConnectionString = "Host=localhost;Database=mini_erp_design_time";

    public static DbContextOptions<TContext> Create<TContext>() where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(DesignTimeConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    public static ITenantContext TenantContext { get; } = new NoTenant();

    private sealed class NoTenant : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("There is no tenant at design time.");
    }
}
