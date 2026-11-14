using MiniErp.Platform.Database;

namespace MiniErp.Sales.Customers;

/// <summary>
/// A company a tenant sells to. BC equivalent: Customer. The number comes from the tenant's
/// CUST series and never changes; documents copy name, tax id and address (BR-08).
/// </summary>
internal sealed class Customer : ITenantOwned
{
    public const int MaxNoLength = 20;
    public const int MaxNameLength = 100;
    public const int MaxAddressLength = 200;
    public const int MaxEmailLength = 254;

    // For EF Core, which materialises rows through this constructor and private setters.
    private Customer()
    {
        No = string.Empty;
        Name = string.Empty;
        TaxId = string.Empty;
        RegistrationNo = string.Empty;
        Address = string.Empty;
    }

    public Customer(string no, CustomerDetails details)
    {
        Id = Guid.CreateVersion7();
        No = no;
        Name = details.Name;
        TaxId = details.TaxId;
        RegistrationNo = details.RegistrationNo;
        Address = details.Address;
        Email = details.Email;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Set by the tenant interceptor when the row is inserted.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Customer number, e.g. CUST-00001.</summary>
    public string No { get; private set; }

    public string Name { get; private set; }

    /// <summary>PIB: 9 digits with a check digit (BR-15). Unique per tenant.</summary>
    public string TaxId { get; private set; }

    /// <summary>Matični broj: 8 digits (BR-15).</summary>
    public string RegistrationNo { get; private set; }

    public string Address { get; private set; }

    public string? Email { get; private set; }

    /// <summary>A blocked customer gets no new orders or releases (BR-11). Customers are never deleted.</summary>
    public bool IsBlocked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>PostgreSQL's row version (xmin). Sent to clients as the ETag (ADR-0002).</summary>
    public uint Version { get; private set; }

    public void Update(CustomerDetails details)
    {
        Name = details.Name;
        TaxId = details.TaxId;
        RegistrationNo = details.RegistrationNo;
        Address = details.Address;
        Email = details.Email;
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

/// <summary>The editable fields of a customer, already validated and normalised.</summary>
internal sealed record CustomerDetails(string Name, string TaxId, string RegistrationNo, string Address, string? Email);
