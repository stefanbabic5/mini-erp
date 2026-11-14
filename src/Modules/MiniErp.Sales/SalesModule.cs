using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Platform;

namespace MiniErp.Sales;

/// <summary>
/// Entry point of the Sales module: the API host calls these two methods and nothing else.
/// </summary>
public static class SalesModule
{
    public const string Name = "sales";

    /// <summary>Registers the module's services in the dependency injection container.</summary>
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>Maps the module's endpoints under <c>/api/sales</c>.</summary>
    public static IEndpointRouteBuilder MapSalesModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleGroup(Name);
        return endpoints;
    }
}
