using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MiniErp.IntegrationTests;

/// <summary>
/// Token validation and the fallback policy (ADR-0003, ADR-0006): every endpoint that does not
/// opt out needs a valid token from the trusted issuer, for this API, with a tenant.
/// </summary>
public class AuthenticationTests(ApiFactory api)
{
    private const string MeUrl = "/api/platform/me";

    private sealed record CurrentUser(string? UserId, string? UserName, Guid TenantId, string[] Roles, string[] Permissions);

    [Fact]
    public async Task Request_without_token_answers_401_as_problem_details()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Valid_token_identifies_user_tenant_roles_and_permissions()
    {
        using var client = api.CreateClientWithToken(
            TestTokens.Create(TestTokens.TenantAlfa.ToString(), ["Sales"], userName: "alfa.sales"));

        var me = await client.GetFromJsonAsync<CurrentUser>(MeUrl, TestContext.Current.CancellationToken);

        me.ShouldNotBeNull();
        me.UserName.ShouldBe("alfa.sales");
        me.TenantId.ShouldBe(TestTokens.TenantAlfa);
        me.Roles.ShouldBe(["Sales"]);
        me.Permissions.ShouldBe(["catalog.read", "sales.customers.read", "sales.customers.write"]);
    }

    [Fact]
    public async Task Token_without_tenant_answers_403()
    {
        using var client = api.CreateClientWithToken(TestTokens.Create(tenantId: null, ["Admin"]));

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Token_with_invalid_tenant_answers_403(string tenantId)
    {
        using var client = api.CreateClientWithToken(TestTokens.Create(tenantId, ["Admin"]));

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Expired_token_answers_401()
    {
        var expired = TestTokens.Create(TestTokens.TenantAlfa.ToString(), ["Admin"], expires: DateTime.UtcNow.AddMinutes(-10));
        using var client = api.CreateClientWithToken(expired);

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_for_another_audience_answers_401()
    {
        var otherAudience = TestTokens.Create(TestTokens.TenantAlfa.ToString(), ["Admin"], audience: "some-other-api");
        using var client = api.CreateClientWithToken(otherAudience);

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_with_an_untrusted_key_answers_401()
    {
        var forged = TestTokens.Create(TestTokens.TenantAlfa.ToString(), ["Admin"], signWithUntrustedKey: true);
        using var client = api.CreateClientWithToken(forged);

        var response = await client.GetAsync(MeUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
