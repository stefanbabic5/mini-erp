using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MiniErp.Platform.Database;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MiniErp.IntegrationTests.ApiFactory))]

namespace MiniErp.IntegrationTests;

/// <summary>
/// Starts one PostgreSQL container for the whole test run and hosts the real API in memory,
/// pointed at that container and trusting <see cref="TestTokens"/> instead of Keycloak.
/// Tests receive it through their constructor.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as compose.yaml, so tests run against the database version used locally.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.7-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    /// <summary>A client that sends a token for the given tenant and roles.</summary>
    public HttpClient CreateClientFor(Guid tenantId, params string[] roles) =>
        CreateClientWithToken(TestTokens.For(tenantId, roles));

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{DatabaseRegistration.ConnectionStringName}", _postgres.GetConnectionString());

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // A fixed configuration instead of Keycloak's discovery document: the API
                // validates tokens exactly as in production, only against the test issuer and key.
                // The handler reads keys through ConfigurationManager (which would call Keycloak),
                // so the test configuration is served by a static one.
                var configuration = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                configuration.SigningKeys.Add(TestTokens.SigningKey);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
                options.TokenValidationParameters.ValidAudience = TestTokens.Audience;
            }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
