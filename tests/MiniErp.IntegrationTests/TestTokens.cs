using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Platform.Security;

namespace MiniErp.IntegrationTests;

/// <summary>
/// Issues access tokens for tests, shaped like Keycloak's (ADR-0006) but signed with a test
/// key that only the test host trusts. Tests therefore never need Keycloak running.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://tests.mini-erp.invalid";
    public const string Audience = "mini-erp-api";

    // Same tenant ids as deploy/keycloak/realm-mini-erp.json.
    public static readonly Guid TenantAlfa = Guid.Parse("a1fa0000-0000-0000-0000-000000000001");
    public static readonly Guid TenantBeta = Guid.Parse("b37a0000-0000-0000-0000-000000000002");

    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("mini-erp integration tests only - not a real signing key"));

    private static readonly SymmetricSecurityKey OtherKey =
        new(Encoding.UTF8.GetBytes("a different key the API does not trust at all, 32+ bytes"));

    public static string For(Guid tenantId, params string[] roles) =>
        Create(tenantId.ToString(), roles);

    /// <summary>Full control over every part of the token, for the negative cases.</summary>
    public static string Create(
        string? tenantId,
        string[] roles,
        string userName = "test.user",
        string audience = Audience,
        DateTime? expires = null,
        bool signWithUntrustedKey = false)
    {
        var claims = new Dictionary<string, object>
        {
            [MiniErpClaims.Subject] = Guid.NewGuid().ToString(),
            [MiniErpClaims.UserName] = userName,
            [MiniErpClaims.Roles] = roles,
        };
        if (tenantId is not null)
        {
            claims[MiniErpClaims.TenantId] = tenantId;
        }

        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = expiresAt.AddMinutes(-10),
            NotBefore = expiresAt.AddMinutes(-10),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                signWithUntrustedKey ? OtherKey : SigningKey,
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>A principal shaped like the one the API builds from a valid token.</summary>
    public static ClaimsPrincipal Principal(Guid? tenantId, params string[] roles)
    {
        var claims = new List<Claim>();
        if (tenantId is not null)
        {
            claims.Add(new Claim(MiniErpClaims.TenantId, tenantId.Value.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(MiniErpClaims.Roles, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test", MiniErpClaims.UserName, MiniErpClaims.Roles));
    }
}
