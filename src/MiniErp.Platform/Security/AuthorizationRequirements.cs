using Microsoft.AspNetCore.Authorization;

namespace MiniErp.Platform.Security;

/// <summary>The user belongs to a tenant: the token carries a valid <c>tenant_id</c> (ADR-0003).</summary>
internal sealed class TenantRequirement : IAuthorizationRequirement;

/// <summary>The user's roles grant <see cref="Permission"/> (see <see cref="RolePermissions"/>).</summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class TenantRequirementHandler : AuthorizationHandler<TenantRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        if (context.User.FindTenantId() is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (RolePermissions.For(context.User.FindRoles()).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
