using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MiniErp.Platform.Database;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MiniErp.IntegrationTests.ApiFactory))]

namespace MiniErp.IntegrationTests;

/// <summary>
/// Starts one PostgreSQL container for the whole test run and hosts the real API in memory,
/// pointed at that container. Tests receive it through their constructor.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as compose.yaml, so tests run against the database version used locally.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.7-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{DatabaseRegistration.ConnectionStringName}", _postgres.GetConnectionString());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
