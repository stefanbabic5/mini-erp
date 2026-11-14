using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Platform;

namespace MiniErp.Invoicing;

/// <summary>
/// Entry point of the Invoicing module: the API host calls these two methods and nothing else.
/// </summary>
public static class InvoicingModule
{
    public const string Name = "invoicing";

    /// <summary>Registers the module's services in the dependency injection container.</summary>
    public static IServiceCollection AddInvoicingModule(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>Maps the module's endpoints under <c>/api/invoicing</c>.</summary>
    public static IEndpointRouteBuilder MapInvoicingModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleGroup(Name);
        return endpoints;
    }
}
