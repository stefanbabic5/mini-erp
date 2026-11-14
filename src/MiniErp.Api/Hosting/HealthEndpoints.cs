using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MiniErp.Platform.Database;

namespace MiniErp.Api.Hosting;

internal static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependencies checked), so an orchestrator
    /// restarts the API only when the API itself is broken.
    /// <c>/health/ready</c>: dependencies such as PostgreSQL answer, so traffic is sent to the
    /// API only when it can actually serve requests.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Probes are called by infrastructure without a token, so they opt out of the
        // fallback authorization policy.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DatabaseRegistration.ReadyTag),
        }).AllowAnonymous();

        return endpoints;
    }
}
