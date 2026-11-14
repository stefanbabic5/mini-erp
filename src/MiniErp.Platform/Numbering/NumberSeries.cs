using System.Globalization;
using MiniErp.Platform.Database;

namespace MiniErp.Platform.Numbering;

/// <summary>
/// One tenant's counter for one kind of document, e.g. customers: <c>CUST-00042</c>.
/// BC equivalent: No. Series + No. Series Line.
/// </summary>
internal sealed class NumberSeries : ITenantOwned
{
    public const int MinDigits = 3;
    public const int MaxDigits = 10;
    public const int MaxPrefixLength = 10;

    // For EF Core, which materialises rows through this constructor and private setters.
    private NumberSeries()
    {
        Code = string.Empty;
        Prefix = string.Empty;
    }

    public NumberSeries(string code, string prefix, int digits)
    {
        Code = code;
        Prefix = prefix;
        Digits = digits;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Set by the tenant interceptor when the row is inserted.</summary>
    public Guid TenantId { get; private set; }

    public string Code { get; private set; }

    public string Prefix { get; private set; }

    public int Digits { get; private set; }

    /// <summary>The last number handed out; 0 when none has been.</summary>
    public long LastNo { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string NextNumberPreview => Format(Prefix, Digits, LastNo + 1);

    public void ChangeFormat(string prefix, int digits)
    {
        Prefix = prefix;
        Digits = digits;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string Format(string prefix, int digits, long number) =>
        prefix + number.ToString("D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
