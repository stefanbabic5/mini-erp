using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MiniErp.Platform;

/// <summary>
/// The one convention every module follows for its HTTP endpoints.
/// </summary>
public static class ModuleEndpoints
{
    /// <summary>
    /// Creates the route group for a module: <c>/api/{moduleName}</c>, tagged with the
    /// module name so that the OpenAPI document groups the module's endpoints together.
    /// </summary>
    public static RouteGroupBuilder MapModuleGroup(this IEndpointRouteBuilder endpoints, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        return endpoints
            .MapGroup($"/api/{moduleName}")
            .WithTags(moduleName);
    }
}
