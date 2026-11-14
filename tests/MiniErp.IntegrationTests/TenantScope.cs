using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.IntegrationTests;

/// <summary>
/// A DI scope that behaves like one HTTP request of the given tenant, for tests that call
/// services or DbContexts directly instead of going through HTTP.
/// </summary>
public sealed class TenantScope : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    private readonly IHttpContextAccessor _accessor;

    private TenantScope(IServiceProvider services, Guid tenantId, string[] roles)
    {
        _scope = services.CreateAsyncScope();
        _accessor = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        _accessor.HttpContext = new DefaultHttpContext
        {
            User = TestTokens.Principal(tenantId, roles),
            RequestServices = _scope.ServiceProvider,
        };
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public static TenantScope For(ApiFactory api, Guid tenantId, params string[] roles) =>
        new(api.Services, tenantId, roles);

    public async ValueTask DisposeAsync()
    {
        _accessor.HttpContext = null;
        await _scope.DisposeAsync();
    }
}
