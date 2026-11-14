using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MiniErp.Platform.Http;
using MiniErp.Platform.Numbering;
using MiniErp.Platform.Security;
using Npgsql;

namespace MiniErp.Sales.Customers;

/// <summary>
/// <c>/api/sales/customers</c>, built the same way as Catalog items (see CLAUDE.md, endpoint
/// conventions). Every query goes through the tenant filter, so another tenant's customer is
/// simply not found (404, BR-13).
/// </summary>
internal static class CustomerEndpoints
{
    private const int DefaultTake = 50;
    private const int MaxTake = 100;

    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder sales)
    {
        var customers = sales.MapGroup("/customers");

        customers.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.CustomersRead)
            .WithName("ListCustomers")
            .WithSummary("Customers of the tenant, ordered by number; search matches number, name or PIB.");

        customers.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Permissions.CustomersRead)
            .WithName("GetCustomer")
            .WithSummary("One customer. The ETag header is needed to update it.");

        customers.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.CustomersWrite)
            .WithName("CreateCustomer")
            .WithSummary("Creates a customer; the number comes from the tenant's CUST series.");

        customers.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(Permissions.CustomersWrite)
            .WithName("UpdateCustomer")
            .WithSummary("Changes the customer's details. Requires If-Match with the customer's ETag.");

        customers.MapPost("/{id:guid}/block", BlockAsync)
            .RequireAuthorization(Permissions.CustomersWrite)
            .WithName("BlockCustomer")
            .WithSummary("Blocks the customer for new orders (BR-11). Customers are never deleted.");

        customers.MapPost("/{id:guid}/unblock", UnblockAsync)
            .RequireAuthorization(Permissions.CustomersWrite)
            .WithName("UnblockCustomer");

        return sales;
    }

    private static async Task<Ok<CustomerPage>> ListAsync(
        SalesDbContext db,
        string? search,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        var offset = Math.Max(skip ?? 0, 0);
        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.No, pattern) || EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.TaxId, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderBy(c => c.No)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new CustomerPage(page.Select(CustomerResponse.From).ToList(), offset, limit, total));
    }

    private static async Task<Results<Ok<CustomerResponse>, NotFound>> GetAsync(
        Guid id,
        SalesDbContext db,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        ConcurrencyHeaders.SetETag(response, customer.Version);
        return TypedResults.Ok(CustomerResponse.From(customer));
    }

    private static async Task<Results<Created<CustomerResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        CustomerRequest request,
        SalesDbContext db,
        INumberSeriesService numberSeries,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var (details, errors) = CustomerValidation.Validate(request);
        if (details is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Taken in its own short transaction: if saving fails below, the number is skipped,
        // which customer numbers allow (ADR-0009).
        var no = await numberSeries.NextAsync(NumberSeriesCodes.Customer, cancellationToken);
        var customer = new Customer(no, details);
        db.Customers.Add(customer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateTaxId(exception))
        {
            return DuplicateTaxId(details.TaxId);
        }

        ConcurrencyHeaders.SetETag(response, customer.Version);
        return TypedResults.Created($"/api/sales/customers/{customer.Id}", CustomerResponse.From(customer));
    }

    private static async Task<Results<Ok<CustomerResponse>, NotFound, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        Guid id,
        CustomerRequest request,
        SalesDbContext db,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (!ConcurrencyHeaders.TryReadIfMatch(httpRequest, out var expectedVersion, out var preconditionProblem))
        {
            return preconditionProblem!;
        }

        if (customer.Version != expectedVersion)
        {
            return ConcurrencyHeaders.PreconditionFailed();
        }

        var (details, errors) = CustomerValidation.Validate(request);
        if (details is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        customer.Update(details);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyHeaders.PreconditionFailed();
        }
        catch (DbUpdateException exception) when (IsDuplicateTaxId(exception))
        {
            return DuplicateTaxId(details.TaxId);
        }

        ConcurrencyHeaders.SetETag(response, customer.Version);
        return TypedResults.Ok(CustomerResponse.From(customer));
    }

    private static Task<Results<NoContent, NotFound>> BlockAsync(Guid id, SalesDbContext db, CancellationToken cancellationToken) =>
        ChangeBlockingAsync(id, db, block: true, cancellationToken);

    private static Task<Results<NoContent, NotFound>> UnblockAsync(Guid id, SalesDbContext db, CancellationToken cancellationToken) =>
        ChangeBlockingAsync(id, db, block: false, cancellationToken);

    private static async Task<Results<NoContent, NotFound>> ChangeBlockingAsync(
        Guid id,
        SalesDbContext db,
        bool block,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (block)
        {
            customer.Block();
        }
        else
        {
            customer.Unblock();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static bool IsDuplicateTaxId(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_customers_tenant_id_tax_id",
        };

    private static ProblemHttpResult DuplicateTaxId(string taxId) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "A customer with this PIB already exists.",
            detail: $"PIB {taxId} is already used by another customer of this tenant.",
            extensions: new Dictionary<string, object?> { ["code"] = "sales.duplicate_tax_id" });
}
