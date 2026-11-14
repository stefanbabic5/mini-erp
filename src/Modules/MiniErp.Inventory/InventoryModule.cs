using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Platform;

namespace MiniErp.Inventory;

/// <summary>
/// Entry point of the Inventory module: the API host calls these two methods and nothing else.
/// </summary>
public static class InventoryModule
{
    public const string Name = "inventory";

    /// <summary>Registers the module's services in the dependency injection container.</summary>
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>Maps the module's endpoints under <c>/api/inventory</c>.</summary>
    public static IEndpointRouteBuilder MapInventoryModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleGroup(Name);
        return endpoints;
    }
}
