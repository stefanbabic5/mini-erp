using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MiniErp.Platform.Security;

namespace MiniErp.Platform.Numbering;

/// <summary>
/// <c>/api/platform/number-series</c>: an Admin sees and changes the format of the tenant's
/// number series. Numbers themselves are only handed out by <see cref="INumberSeriesService"/>.
/// </summary>
internal static partial class NumberSeriesEndpoints
{
    public sealed record NumberSeriesResponse(string Code, string Prefix, int Digits, long LastNo, string NextNumber);

    public sealed record UpdateNumberSeriesRequest(string? Prefix, int? Digits);

    public static IEndpointRouteBuilder MapNumberSeriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup("platform")
            .MapGroup("/number-series")
            .RequireAuthorization(Permissions.NumberSeriesManage);

        group.MapGet("/", ListAsync)
            .WithName("ListNumberSeries")
            .WithSummary("Every number series of the tenant, with defaults for those not used yet.");

        group.MapPut("/{code}", UpdateAsync)
            .WithName("UpdateNumberSeries")
            .WithSummary("Changes the prefix and number of digits of a series. The counter is not changed.");

        return endpoints;
    }

    private static async Task<Ok<List<NumberSeriesResponse>>> ListAsync(PlatformDbContext db, CancellationToken cancellationToken)
    {
        // The tenant query filter limits this to the caller's tenant.
        var stored = await db.NumberSeries.AsNoTracking().ToDictionaryAsync(s => s.Code, cancellationToken);

        var series = NumberSeriesCodes.Defaults
            .Select(entry => stored.TryGetValue(entry.Key, out var existing)
                ? ToResponse(existing)
                : new NumberSeriesResponse(
                    entry.Key,
                    entry.Value.Prefix,
                    entry.Value.Digits,
                    LastNo: 0,
                    NumberSeries.Format(entry.Value.Prefix, entry.Value.Digits, 1)))
            .OrderBy(response => response.Code, StringComparer.Ordinal)
            .ToList();

        return TypedResults.Ok(series);
    }

    private static async Task<Results<Ok<NumberSeriesResponse>, ValidationProblem, NotFound>> UpdateAsync(
        string code,
        UpdateNumberSeriesRequest request,
        PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        if (!NumberSeriesCodes.IsKnown(code))
        {
            return TypedResults.NotFound();
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.Code == code, cancellationToken);
        if (series is null)
        {
            series = new NumberSeries(code, request.Prefix!, request.Digits!.Value);
            db.NumberSeries.Add(series);
        }
        else
        {
            series.ChangeFormat(request.Prefix!, request.Digits!.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(series));
    }

    private static Dictionary<string, string[]> Validate(UpdateNumberSeriesRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Prefix is null)
        {
            errors["prefix"] = ["Prefix is required (it may be empty)."];
        }
        else if (request.Prefix.Length > NumberSeries.MaxPrefixLength || !PrefixPattern().IsMatch(request.Prefix))
        {
            errors["prefix"] = [$"Prefix must be at most {NumberSeries.MaxPrefixLength} characters: A-Z, 0-9, '-' or '/'."];
        }

        if (request.Digits is not (>= NumberSeries.MinDigits and <= NumberSeries.MaxDigits))
        {
            errors["digits"] = [$"Digits must be between {NumberSeries.MinDigits} and {NumberSeries.MaxDigits}."];
        }

        return errors;
    }

    private static NumberSeriesResponse ToResponse(NumberSeries series) =>
        new(series.Code, series.Prefix, series.Digits, series.LastNo, series.NextNumberPreview);

    [GeneratedRegex("^[A-Z0-9/-]*$")]
    private static partial Regex PrefixPattern();
}
