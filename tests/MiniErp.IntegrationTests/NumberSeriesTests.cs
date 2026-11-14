using MiniErp.Platform.Numbering;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary>
/// Series where gaps are allowed (BR-09, ADR-0009): customers, orders, shipments.
/// Numbers are unique per tenant and series, also under concurrency.
/// </summary>
public class NumberSeriesTests(ApiFactory api)
{
    [Fact]
    public async Task First_numbers_use_the_default_format_and_count_up()
    {
        await using var scope = TenantScope.For(api, Guid.NewGuid());
        var series = scope.Get<INumberSeriesService>();

        var first = await series.NextAsync(NumberSeriesCodes.Customer, TestContext.Current.CancellationToken);
        var second = await series.NextAsync(NumberSeriesCodes.Customer, TestContext.Current.CancellationToken);

        first.ShouldBe("CUST-00001");
        second.ShouldBe("CUST-00002");
    }

    [Fact]
    public async Task BR13_each_tenant_has_its_own_counter()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var a = TenantScope.For(api, tenantA))
        {
            await a.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.SalesOrder, TestContext.Current.CancellationToken);
            await a.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.SalesOrder, TestContext.Current.CancellationToken);
        }

        await using var b = TenantScope.For(api, tenantB);
        var firstForB = await b.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.SalesOrder, TestContext.Current.CancellationToken);

        firstForB.ShouldBe("SO-00001");
    }

    [Fact]
    public async Task BR09_parallel_requests_never_get_the_same_number()
    {
        const int requests = 50;
        var tenant = Guid.NewGuid();

        // Each task is its own scope and connection, like 50 simultaneous HTTP requests.
        var numbers = await Task.WhenAll(Enumerable.Range(0, requests).Select(async _ =>
        {
            await using var scope = TenantScope.For(api, tenant);
            return await scope.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.Shipment, TestContext.Current.CancellationToken);
        }));

        numbers.ShouldBeUnique();
        numbers.Order(StringComparer.Ordinal).ShouldBe(
            Enumerable.Range(1, requests).Select(n => $"SHIP-{n:D5}"));
    }

    [Fact]
    public async Task Unknown_series_is_refused()
    {
        await using var scope = TenantScope.For(api, Guid.NewGuid());

        await Should.ThrowAsync<ArgumentException>(() =>
            scope.Get<INumberSeriesService>().NextAsync("NOPE", TestContext.Current.CancellationToken));
    }
}
