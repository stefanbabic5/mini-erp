using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Platform;

namespace MiniErp.Catalog;

/// <summary>
/// Entry point of the Catalog module: the API host calls these two methods and nothing else.
/// </summary>
public static class CatalogModule
{
    public const string Name = "catalog";

    /// <summary>Registers the module's services in the dependency injection container.</summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>Maps the module's endpoints under <c>/api/catalog</c>.</summary>
    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleGroup(Name);
        return endpoints;
    }
}
