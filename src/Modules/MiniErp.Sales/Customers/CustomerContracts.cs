using System.Net.Mail;

namespace MiniErp.Sales.Customers;

// Request and response shapes of /api/sales/customers. Create and update take the same fields;
// the number is assigned by the server from the tenant's CUST series.

internal sealed record CustomerRequest(string? Name, string? TaxId, string? RegistrationNo, string? Address, string? Email);

internal sealed record CustomerResponse(
    Guid Id,
    string No,
    string Name,
    string TaxId,
    string RegistrationNo,
    string Address,
    string? Email,
    bool IsBlocked,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CustomerResponse From(Customer customer) => new(
        customer.Id,
        customer.No,
        customer.Name,
        customer.TaxId,
        customer.RegistrationNo,
        customer.Address,
        customer.Email,
        customer.IsBlocked,
        customer.CreatedAt,
        customer.UpdatedAt);
}

internal sealed record CustomerPage(IReadOnlyList<CustomerResponse> Customers, int Skip, int Take, int Total);

/// <summary>Field rules for customers. Errors are keyed by field name for a 400 validation problem.</summary>
internal static class CustomerValidation
{
    /// <summary>Validates and normalises the request (trimmed text, empty email treated as none).</summary>
    public static (CustomerDetails? Details, Dictionary<string, string[]> Errors) Validate(CustomerRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > Customer.MaxNameLength)
        {
            errors["name"] = [$"Name must be at most {Customer.MaxNameLength} characters."];
        }

        var taxId = request.TaxId?.Trim();
        if (!SerbianCompanyIds.IsValidTaxId(taxId))
        {
            errors["taxId"] = ["PIB must be 9 digits with a valid check digit (BR-15)."];
        }

        var registrationNo = request.RegistrationNo?.Trim();
        if (!SerbianCompanyIds.IsValidRegistrationNo(registrationNo))
        {
            errors["registrationNo"] = ["Registration number (matični broj) must be 8 digits (BR-15)."];
        }

        var address = request.Address?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            errors["address"] = ["Address is required."];
        }
        else if (address.Length > Customer.MaxAddressLength)
        {
            errors["address"] = [$"Address must be at most {Customer.MaxAddressLength} characters."];
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && (email.Length > Customer.MaxEmailLength || !MailAddress.TryCreate(email, out _)))
        {
            errors["email"] = ["Email is not a valid address."];
        }

        return errors.Count > 0
            ? (null, errors)
            : (new CustomerDetails(name!, taxId!, registrationNo!, address!, email), errors);
    }
}
