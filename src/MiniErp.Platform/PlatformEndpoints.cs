using Microsoft.AspNetCore.Routing;
using MiniErp.Platform.Numbering;
using MiniErp.Platform.Security;

namespace MiniErp.Platform;

public static class PlatformEndpoints
{
    /// <summary>Maps Platform's own endpoints under <c>/api/platform</c>.</summary>
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCurrentUserEndpoint();
        endpoints.MapNumberSeriesEndpoints();
        return endpoints;
    }
}
