namespace MiniErp.Catalog.Items;

// Request and response shapes of /api/catalog/items. Nullable request fields let validation
// report a missing value as a field error instead of a deserialisation failure.

internal sealed record CreateItemRequest(string? Sku, string? Name, decimal? UnitPrice, VatRate? VatRate);

internal sealed record UpdateItemRequest(string? Name, decimal? UnitPrice, VatRate? VatRate);

internal sealed record ItemResponse(
    Guid Id,
    string Sku,
    string Name,
    decimal UnitPrice,
    VatRate VatRate,
    int VatPercent,
    bool IsBlocked,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ItemResponse From(Item item) => new(
        item.Id,
        item.Sku,
        item.Name,
        item.UnitPrice,
        item.VatRate,
        (int)item.VatRate,
        item.IsBlocked,
        item.CreatedAt,
        item.UpdatedAt);
}

internal sealed record ItemPage(IReadOnlyList<ItemResponse> Items, int Skip, int Take, int Total);
