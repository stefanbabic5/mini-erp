namespace MiniErp.Platform.Numbering;

/// <summary>
/// The document number series the system knows (specification §4.5, ADR-0009), with the
/// format a tenant's series starts with. A tenant can change prefix and digits later.
/// </summary>
public static class NumberSeriesCodes
{
    public const string Customer = "CUST";
    public const string SalesOrder = "SO";
    public const string Shipment = "SHIP";

    public sealed record Format(string Prefix, int Digits);

    public static readonly IReadOnlyDictionary<string, Format> Defaults = new Dictionary<string, Format>(StringComparer.Ordinal)
    {
        [Customer] = new("CUST-", 5),
        [SalesOrder] = new("SO-", 5),
        [Shipment] = new("SHIP-", 5),
    };

    public static bool IsKnown(string code) => Defaults.ContainsKey(code);
}
