using System.Net;
using Microsoft.AspNetCore.Hosting;
using MiniErp.Platform.Database;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary>
/// The hosting basics every later feature relies on: health probes, the OpenAPI document,
/// and Problem Details for errors.
/// </summary>
public class HostingTests(ApiFactory api)
{
    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Liveness_probe_answers_healthy()
    {
        var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Readiness_probe_answers_healthy_when_postgres_is_reachable()
    {
        var response = await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Readiness_probe_answers_503_when_postgres_is_unreachable_while_liveness_stays_healthy()
    {
        // Same API, but pointed at a port where no database listens.
        using var apiWithoutDatabase = api.WithWebHostBuilder(builder => builder.UseSetting(
            $"ConnectionStrings:{DatabaseRegistration.ConnectionStringName}",
            "Host=localhost;Port=1;Database=none;Username=none;Password=none;Timeout=2"));
        using var client = apiWithoutDatabase.CreateClient();

        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_served_in_development()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task Unknown_route_answers_404_as_problem_details()
    {
        using var client = api.CreateClientFor(TestTokens.TenantAlfa, "Admin");

        var response = await client.GetAsync("/api/catalog/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
