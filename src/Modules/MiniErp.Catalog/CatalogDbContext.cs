using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniErp.Catalog.Items;
using MiniErp.Platform.Database;
using MiniErp.Platform.Security;

namespace MiniErp.Catalog;

/// <summary>The Catalog module's tables, in the <c>catalog</c> schema.</summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "catalog";

    protected override string Schema => SchemaName;

    public DbSet<Item> Items => Set<Item>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(item =>
        {
            item.ToTable("items");
            item.HasKey(i => i.Id);
            item.HasIndex(i => new { i.TenantId, i.Sku }).IsUnique();
            item.Property(i => i.Sku).HasMaxLength(Item.MaxSkuLength);
            item.Property(i => i.Name).HasMaxLength(Item.MaxNameLength);
            item.Property(i => i.UnitPrice).HasPrecision(18, 2);
            item.Property(i => i.VatRate).HasConversion<string>().HasMaxLength(10);
            item.Property(i => i.Version).IsRowVersion();
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context to generate migrations.</summary>
internal sealed class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Create<CatalogDbContext>(), DesignTimeDbContextOptions.TenantContext);
}
