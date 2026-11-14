using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MiniErp.Platform.Http;

/// <summary>
/// Optimistic concurrency over HTTP (specification §7): responses carry the row version as an
/// <c>ETag</c>; an update must send it back in <c>If-Match</c>. If someone else changed the row in
/// between, the versions differ and the update is refused with 412, instead of silently
/// overwriting their change ("lost update").
/// </summary>
public static class ConcurrencyHeaders
{
    public static string ToETag(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static void SetETag(HttpResponse response, uint version) => response.Headers.ETag = ToETag(version);

    /// <summary>
    /// Reads the version from <c>If-Match</c>. Returns a problem result when the header is
    /// missing (428 Precondition Required) or not a version this API issued (412).
    /// </summary>
    public static bool TryReadIfMatch(HttpRequest request, out uint version, out ProblemHttpResult? problem)
    {
        version = 0;
        problem = null;

        var header = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            problem = TypedResults.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "If-Match header is required.",
                detail: "Send the ETag from the last GET of this resource, so a concurrent change is not overwritten.");
            return false;
        }

        var value = header.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        if (!uint.TryParse(value.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version))
        {
            problem = PreconditionFailed();
            return false;
        }

        return true;
    }

    public static ProblemHttpResult PreconditionFailed() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status412PreconditionFailed,
            title: "The resource was changed by someone else.",
            detail: "Read it again to get the current version and ETag, then repeat the change.");
}
