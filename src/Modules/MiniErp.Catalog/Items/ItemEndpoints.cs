using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MiniErp.Platform.Http;
using MiniErp.Platform.Security;
using Npgsql;

namespace MiniErp.Catalog.Items;

/// <summary>
/// <c>/api/catalog/items</c>. Every query goes through the tenant filter, so another tenant's
/// item is simply not found (404, BR-13).
/// </summary>
internal static class ItemEndpoints
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    public static RouteGroupBuilder MapItemEndpoints(this RouteGroupBuilder catalog)
    {
        var items = catalog.MapGroup("/items");

        items.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.CatalogRead)
            .WithName("ListItems")
            .WithSummary("Items of the tenant, ordered by SKU; search matches SKU or name.");

        items.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Permissions.CatalogRead)
            .WithName("GetItem")
            .WithSummary("One item. The ETag header is needed to update it.");

        items.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.CatalogWrite)
            .WithName("CreateItem");

        items.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(Permissions.CatalogWrite)
            .WithName("UpdateItem")
            .WithSummary("Changes name, price and VAT rate. Requires If-Match with the item's ETag.");

        items.MapPost("/{id:guid}/block", BlockAsync)
            .RequireAuthorization(Permissions.CatalogWrite)
            .WithName("BlockItem")
            .WithSummary("Blocks the item for new orders (BR-11). Items are never deleted (BR-12).");

        items.MapPost("/{id:guid}/unblock", UnblockAsync)
            .RequireAuthorization(Permissions.CatalogWrite)
            .WithName("UnblockItem");

        return catalog;
    }

    private static async Task<Ok<ItemPage>> ListAsync(
        CatalogDbContext db,
        string? search,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        var offset = Math.Max(skip ?? 0, 0);
        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

        var query = db.Items.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.Sku, pattern) || EF.Functions.ILike(i.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderBy(i => i.Sku)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ItemPage(page.Select(ItemResponse.From).ToList(), offset, limit, total));
    }

    private static async Task<Results<Ok<ItemResponse>, NotFound>> GetAsync(
        Guid id,
        CatalogDbContext db,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        ConcurrencyHeaders.SetETag(response, item.Version);
        return TypedResults.Ok(ItemResponse.From(item));
    }

    private static async Task<Results<Created<ItemResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        CreateItemRequest request,
        CatalogDbContext db,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var errors = ItemValidation.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var item = new Item(request.Sku!, request.Name!.Trim(), request.UnitPrice!.Value, request.VatRate!.Value);
        db.Items.Add(item);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "An item with this SKU already exists.",
                detail: $"SKU '{request.Sku}' is already used in this tenant.",
                extensions: new Dictionary<string, object?> { ["code"] = "catalog.duplicate_sku" });
        }

        ConcurrencyHeaders.SetETag(response, item.Version);
        return TypedResults.Created($"/api/catalog/items/{item.Id}", ItemResponse.From(item));
    }

    private static async Task<Results<Ok<ItemResponse>, NotFound, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateItemRequest request,
        CatalogDbContext db,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (!ConcurrencyHeaders.TryReadIfMatch(httpRequest, out var expectedVersion, out var preconditionProblem))
        {
            return preconditionProblem!;
        }

        if (item.Version != expectedVersion)
        {
            return ConcurrencyHeaders.PreconditionFailed();
        }

        var errors = ItemValidation.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        item.Update(request.Name!.Trim(), request.UnitPrice!.Value, request.VatRate!.Value);

        try
        {
            // EF Core adds "WHERE xmin = <version read above>" to the UPDATE, so a change that
            // happened after our read (even after the check above) makes this fail.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHeaders.PreconditionFailed();
        }

        ConcurrencyHeaders.SetETag(response, item.Version);
        return TypedResults.Ok(ItemResponse.From(item));
    }

    private static Task<Results<NoContent, NotFound>> BlockAsync(Guid id, CatalogDbContext db, CancellationToken cancellationToken) =>
        ChangeBlockingAsync(id, db, block: true, cancellationToken);

    private static Task<Results<NoContent, NotFound>> UnblockAsync(Guid id, CatalogDbContext db, CancellationToken cancellationToken) =>
        ChangeBlockingAsync(id, db, block: false, cancellationToken);

    private static async Task<Results<NoContent, NotFound>> ChangeBlockingAsync(
        Guid id,
        CatalogDbContext db,
        bool block,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (block)
        {
            item.Block();
        }
        else
        {
            item.Unblock();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
