using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.Platform.Security;

public static class SecurityRegistration
{
    /// <summary>Configuration section with the token settings (<c>Authentication:Jwt</c>).</summary>
    public const string JwtSection = "Authentication:Jwt";

    /// <summary>
    /// JWT bearer authentication against the identity provider (Keycloak, ADR-0006), one
    /// authorization policy per permission, and a fallback policy that makes every endpoint
    /// require an authenticated user with a tenant unless it opts out with AllowAnonymous.
    /// </summary>
    public static IServiceCollection AddMiniErpSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtSection);
        var authority = jwt["Authority"];
        var audience = jwt["Audience"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                $"{JwtSection}:Authority and {JwtSection}:Audience must be configured (see appsettings.Development.json).");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = jwt.GetValue("RequireHttpsMetadata", defaultValue: true);

                // Keep claim names exactly as the token has them (tenant_id, roles, sub)
                // instead of mapping them to long XML-era URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = MiniErpClaims.UserName;
                options.TokenValidationParameters.RoleClaimType = MiniErpClaims.Roles;
            });

        services.AddAuthorization(options =>
        {
            // Secure by default: an endpoint without its own policy still needs a user with a tenant.
            options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new TenantRequirement())
                .Build();

            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy => policy
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TenantRequirement(), new PermissionRequirement(permission)));
            }
        });

        services.AddSingleton<IAuthorizationHandler, TenantRequirementHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionRequirementHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        return services;
    }
}
