using Microsoft.AspNetCore.Http;

namespace MiniErp.Platform.Security;

/// <summary>
/// The tenant the current request works for. Comes only from the access token, never from
/// the URL, a header or the body (ADR-0003).
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant. Throws when there is none, which is a bug: every
    /// non-anonymous endpoint requires a tenant through the fallback authorization policy.</summary>
    Guid TenantId { get; }
}

internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid TenantId =>
        httpContextAccessor.HttpContext?.User.FindTenantId()
        ?? throw new InvalidOperationException("The current request has no tenant.");
}
