namespace MiniErp.Sales.Customers;

/// <summary>
/// Serbian company identifiers (BR-15).
/// </summary>
internal static class SerbianCompanyIds
{
    /// <summary>
    /// PIB (tax id): 9 digits, the last one a check digit computed with ISO 7064 MOD 11,10
    /// over the first 8. Catches every single wrong digit and most swapped neighbours.
    /// </summary>
    public static bool IsValidTaxId(string? pib)
    {
        if (pib is not { Length: 9 } || !pib.All(char.IsAsciiDigit))
        {
            return false;
        }

        var product = 10;
        for (var i = 0; i < 8; i++)
        {
            var sum = (product + (pib[i] - '0')) % 10;
            if (sum == 0)
            {
                sum = 10;
            }

            product = sum * 2 % 11;
        }

        var checkDigit = (11 - product) % 10;
        return pib[8] - '0' == checkDigit;
    }

    /// <summary>Matični broj (registration number): 8 digits.</summary>
    public static bool IsValidRegistrationNo(string? registrationNo) =>
        registrationNo is { Length: 8 } && registrationNo.All(char.IsAsciiDigit);
}
