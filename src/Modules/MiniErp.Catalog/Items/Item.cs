using MiniErp.Platform.Database;

namespace MiniErp.Catalog.Items;

/// <summary>VAT rates in Serbia (specification §4.1). The percentage is part of the meaning, not data.</summary>
internal enum VatRate
{
    Standard = 20,
    Reduced = 10,
    Exempt = 0,
}

/// <summary>
/// Something a tenant sells. BC equivalent: Item. The SKU (BC: No.) is fixed once created,
/// because documents copy it (BR-08); name, price and VAT rate can change.
/// </summary>
internal sealed class Item : ITenantOwned
{
    public const int MaxSkuLength = 20;
    public const int MaxNameLength = 100;

    // For EF Core, which materialises rows through this constructor and private setters.
    private Item()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }

    public Item(string sku, string name, decimal unitPrice, VatRate vatRate)
    {
        Id = Guid.CreateVersion7();
        Sku = sku;
        Name = name;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Set by the tenant interceptor when the row is inserted.</summary>
    public Guid TenantId { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    /// <summary>Price per unit in RSD, without VAT, 2 decimals (BR-10).</summary>
    public decimal UnitPrice { get; private set; }

    public VatRate VatRate { get; private set; }

    /// <summary>A blocked item cannot be added to new orders (BR-11). Items are blocked, never deleted (BR-12).</summary>
    public bool IsBlocked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>PostgreSQL's row version (xmin). Sent to clients as the ETag (ADR-0002).</summary>
    public uint Version { get; private set; }

    public void Update(string name, decimal unitPrice, VatRate vatRate)
    {
        Name = name;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Block()
    {
        IsBlocked = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Unblock()
    {
        IsBlocked = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
