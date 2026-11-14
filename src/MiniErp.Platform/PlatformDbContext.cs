using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniErp.Platform.Database;
using MiniErp.Platform.Numbering;
using MiniErp.Platform.Security;

namespace MiniErp.Platform;

/// <summary>Platform's own tables, in the <c>platform</c> schema.</summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "platform";

    protected override string Schema => SchemaName;

    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NumberSeries>(series =>
        {
            series.ToTable("number_series");
            series.HasKey(s => new { s.TenantId, s.Code });
            series.Property(s => s.Code).HasMaxLength(10);
            series.Property(s => s.Prefix).HasMaxLength(Numbering.NumberSeries.MaxPrefixLength);
            series.Ignore(s => s.NextNumberPreview);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context to generate migrations.</summary>
internal sealed class PlatformDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Create<PlatformDbContext>(), DesignTimeDbContextOptions.TenantContext);
}
