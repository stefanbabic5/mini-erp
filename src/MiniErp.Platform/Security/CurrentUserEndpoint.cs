using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MiniErp.Platform.Security;

/// <summary>Who the API thinks the caller is: <c>GET /api/platform/me</c>.</summary>
public static class CurrentUserEndpoint
{
    public sealed record CurrentUserResponse(
        string? UserId,
        string? UserName,
        Guid TenantId,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions);

    public static IEndpointRouteBuilder MapCurrentUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleGroup("platform")
            .MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("The authenticated user, their tenant, roles and resulting permissions.");

        return endpoints;
    }

    private static CurrentUserResponse GetCurrentUser(ClaimsPrincipal user, ITenantContext tenant)
    {
        var roles = user.FindRoles();

        return new CurrentUserResponse(
            UserId: user.FindFirst(MiniErpClaims.Subject)?.Value,
            UserName: user.Identity?.Name,
            TenantId: tenant.TenantId,
            Roles: roles,
            Permissions: RolePermissions.For(roles).Order(StringComparer.Ordinal).ToList());
    }
}
