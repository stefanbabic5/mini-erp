using System.Text.RegularExpressions;

namespace MiniErp.Catalog.Items;

/// <summary>Field rules for items. Errors are keyed by field name for a 400 validation problem.</summary>
internal static partial class ItemValidation
{
    private const decimal MaxUnitPrice = 9_999_999_999_999_999.99m; // numeric(18,2)

    public static Dictionary<string, string[]> Validate(CreateItemRequest request)
    {
        var errors = Validate(request.Name, request.UnitPrice, request.VatRate);

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            errors["sku"] = ["SKU is required."];
        }
        else if (request.Sku.Length > Item.MaxSkuLength || !SkuPattern().IsMatch(request.Sku))
        {
            errors["sku"] = [$"SKU must be at most {Item.MaxSkuLength} characters: A-Z, 0-9, '.', '-' or '_', starting with a letter or digit."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(UpdateItemRequest request) =>
        Validate(request.Name, request.UnitPrice, request.VatRate);

    private static Dictionary<string, string[]> Validate(string? name, decimal? unitPrice, VatRate? vatRate)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Trim().Length > Item.MaxNameLength)
        {
            errors["name"] = [$"Name must be at most {Item.MaxNameLength} characters."];
        }

        if (unitPrice is null)
        {
            errors["unitPrice"] = ["Unit price is required."];
        }
        else if (unitPrice < 0 || unitPrice > MaxUnitPrice)
        {
            errors["unitPrice"] = ["Unit price must be zero or positive."];
        }
        else if (decimal.Round(unitPrice.Value, 2) != unitPrice.Value)
        {
            errors["unitPrice"] = ["Unit price has at most 2 decimals (BR-10)."];
        }

        if (vatRate is null || !Enum.IsDefined(vatRate.Value))
        {
            errors["vatRate"] = ["VAT rate is required: Standard (20%), Reduced (10%) or Exempt (0%)."];
        }

        return errors;
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex SkuPattern();
}
