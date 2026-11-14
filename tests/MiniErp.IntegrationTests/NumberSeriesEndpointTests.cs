using System.Net;
using System.Net.Http.Json;
using MiniErp.Platform.Numbering;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary><c>/api/platform/number-series</c>: Admin only, scoped to the caller's tenant.</summary>
public class NumberSeriesEndpointTests(ApiFactory api)
{
    private const string Url = "/api/platform/number-series";

    private sealed record Series(string Code, string Prefix, int Digits, long LastNo, string NextNumber);

    [Fact]
    public async Task Admin_sees_every_series_with_defaults_before_first_use()
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var series = await admin.GetFromJsonAsync<Series[]>(Url, TestContext.Current.CancellationToken);

        series.ShouldNotBeNull();
        series.Select(s => s.Code).ShouldBe(["CUST", "SHIP", "SO"]);
        series.Single(s => s.Code == "CUST").NextNumber.ShouldBe("CUST-00001");
    }

    [Fact]
    public async Task Admin_changes_the_format_and_the_next_number_follows_it()
    {
        var tenant = Guid.NewGuid();
        using var admin = api.CreateClientFor(tenant, "Admin");

        var response = await admin.PutAsJsonAsync($"{Url}/CUST", new { prefix = "K", digits = 3 }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var scope = TenantScope.For(api, tenant);
        var next = await scope.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.Customer, TestContext.Current.CancellationToken);
        next.ShouldBe("K001");
    }

    [Fact]
    public async Task BR13_one_tenants_change_is_invisible_to_another_tenant()
    {
        using var alfa = api.CreateClientFor(Guid.NewGuid(), "Admin");
        using var beta = api.CreateClientFor(Guid.NewGuid(), "Admin");

        (await alfa.PutAsJsonAsync($"{Url}/SO", new { prefix = "ALFA-", digits = 4 }, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var betaSeries = await beta.GetFromJsonAsync<Series[]>(Url, TestContext.Current.CancellationToken);

        betaSeries.ShouldNotBeNull();
        betaSeries.Single(s => s.Code == "SO").Prefix.ShouldBe("SO-");
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    [InlineData("Finance")]
    public async Task Non_admins_get_403(string role)
    {
        using var client = api.CreateClientFor(Guid.NewGuid(), role);

        var list = await client.GetAsync(Url, TestContext.Current.CancellationToken);
        var update = await client.PutAsJsonAsync($"{Url}/CUST", new { prefix = "X-", digits = 5 }, TestContext.Current.CancellationToken);

        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        update.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("lower-", 5)]
    [InlineData("TOO-LONG-PREFIX", 5)]
    [InlineData("OK-", 2)]
    [InlineData("OK-", 11)]
    public async Task Invalid_format_answers_400_validation_problem(string prefix, int digits)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PutAsJsonAsync($"{Url}/CUST", new { prefix, digits }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Unknown_series_answers_404()
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PutAsJsonAsync($"{Url}/NOPE", new { prefix = "N-", digits = 5 }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
