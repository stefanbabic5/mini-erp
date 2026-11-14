using Microsoft.EntityFrameworkCore;
using MiniErp.Platform;
using MiniErp.Platform.Database;
using MiniErp.Platform.Numbering;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary>
/// BR-13 at the data layer (ADR-0003): the EF Core query filter hides other tenants' rows,
/// and the SaveChanges interceptor stamps the tenant on inserts and refuses writes to another
/// tenant's rows. Every test uses fresh tenant ids, so tests never see each other's data.
/// </summary>
public class TenantIsolationTests(ApiFactory api)
{
    [Fact]
    public async Task BR13_queries_return_only_the_current_tenants_rows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await CreateSeriesAsync(tenantA, NumberSeriesCodes.Customer, "A-");
        await CreateSeriesAsync(tenantB, NumberSeriesCodes.Customer, "B-");

        await using var scope = TenantScope.For(api, tenantA);
        var rows = await scope.Get<PlatformDbContext>().NumberSeries.ToListAsync(TestContext.Current.CancellationToken);

        rows.ShouldHaveSingleItem().Prefix.ShouldBe("A-");
        rows.Single().TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task BR13_insert_gets_the_current_tenant_without_setting_it()
    {
        var tenant = Guid.NewGuid();

        await using (var scope = TenantScope.For(api, tenant))
        {
            var db = scope.Get<PlatformDbContext>();
            db.NumberSeries.Add(new NumberSeries(NumberSeriesCodes.Shipment, "S-", 4));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = TenantScope.For(api, tenant);
        var stored = await check.Get<PlatformDbContext>().NumberSeries.SingleAsync(TestContext.Current.CancellationToken);
        stored.TenantId.ShouldBe(tenant);
    }

    [Fact]
    public async Task BR13_inserting_a_row_for_another_tenant_is_refused()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var scope = TenantScope.For(api, tenantA);
        var db = scope.Get<PlatformDbContext>();

        var foreign = new NumberSeries(NumberSeriesCodes.Customer, "X-", 5);
        db.Entry(foreign).Property(s => s.TenantId).CurrentValue = tenantB;
        db.NumberSeries.Add(foreign);

        await Should.ThrowAsync<CrossTenantWriteException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BR13_changing_another_tenants_row_is_refused_even_past_the_filter()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await CreateSeriesAsync(tenantB, NumberSeriesCodes.Customer, "B-");

        await using var scope = TenantScope.For(api, tenantA);
        var db = scope.Get<PlatformDbContext>();
        var foreign = await db.NumberSeries.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantB, TestContext.Current.CancellationToken);
        foreign.ChangeFormat("HACKED-", 5);

        await Should.ThrowAsync<CrossTenantWriteException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BR13_deleting_another_tenants_row_is_refused_even_past_the_filter()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await CreateSeriesAsync(tenantB, NumberSeriesCodes.Customer, "B-");

        await using var scope = TenantScope.For(api, tenantA);
        var db = scope.Get<PlatformDbContext>();
        var foreign = await db.NumberSeries.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantB, TestContext.Current.CancellationToken);
        db.NumberSeries.Remove(foreign);

        await Should.ThrowAsync<CrossTenantWriteException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task CreateSeriesAsync(Guid tenantId, string code, string prefix)
    {
        await using var scope = TenantScope.For(api, tenantId);
        var db = scope.Get<PlatformDbContext>();
        db.NumberSeries.Add(new NumberSeries(code, prefix, 5));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
