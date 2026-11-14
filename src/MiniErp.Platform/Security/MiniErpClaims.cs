using System.Security.Claims;

namespace MiniErp.Platform.Security;

/// <summary>
/// Claim names in the access token. Inbound claim mapping is switched off, so these are the
/// names exactly as Keycloak writes them (ADR-0006).
/// </summary>
public static class MiniErpClaims
{
    public const string Subject = "sub";
    public const string UserName = "preferred_username";
    public const string TenantId = "tenant_id";
    public const string Roles = "roles";

    /// <summary>The tenant id from the token, or <c>null</c> when it is missing or not a GUID.</summary>
    public static Guid? FindTenantId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(TenantId)?.Value;
        return Guid.TryParse(value, out var tenantId) && tenantId != Guid.Empty ? tenantId : null;
    }

    public static IReadOnlyList<string> FindRoles(this ClaimsPrincipal user) =>
        user.FindAll(Roles).Select(claim => claim.Value).ToList();
}
