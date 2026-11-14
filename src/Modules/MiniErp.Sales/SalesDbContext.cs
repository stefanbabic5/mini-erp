using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniErp.Platform.Database;
using MiniErp.Platform.Security;
using MiniErp.Sales.Customers;

namespace MiniErp.Sales;

/// <summary>The Sales module's tables, in the <c>sales</c> schema.</summary>
internal sealed class SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    public const string SchemaName = "sales";

    protected override string Schema => SchemaName;

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(c => c.Id);
            customer.HasIndex(c => new { c.TenantId, c.No }).IsUnique();
            customer.HasIndex(c => new { c.TenantId, c.TaxId }).IsUnique();
            customer.Property(c => c.No).HasMaxLength(Customer.MaxNoLength);
            customer.Property(c => c.Name).HasMaxLength(Customer.MaxNameLength);
            customer.Property(c => c.TaxId).HasMaxLength(9).IsFixedLength();
            customer.Property(c => c.RegistrationNo).HasMaxLength(8).IsFixedLength();
            customer.Property(c => c.Address).HasMaxLength(Customer.MaxAddressLength);
            customer.Property(c => c.Email).HasMaxLength(Customer.MaxEmailLength);
            customer.Property(c => c.Version).IsRowVersion();
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context to generate migrations.</summary>
internal sealed class SalesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args) =>
        new(DesignTimeDbContextOptions.Create<SalesDbContext>(), DesignTimeDbContextOptions.TenantContext);
}
