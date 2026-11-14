using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace MiniErp.IntegrationTests.Sales;

/// <summary>
/// <c>/api/sales/customers</c>: numbering from the CUST series, PIB and registration number
/// rules (BR-15), optimistic concurrency, blocking and tenant isolation (BR-13).
/// Every test uses fresh tenants, so they never see each other's customers.
/// </summary>
public class CustomerEndpointTests(ApiFactory api)
{
    private const string Url = "/api/sales/customers";

    // PIBs of real Serbian companies (public registry data), used to prove the check digit rule.
    private const string PibA = "100002887";
    private const string PibB = "104052135";

    internal sealed record Customer(
        Guid Id, string No, string Name, string TaxId, string RegistrationNo, string Address, string? Email, bool IsBlocked);

    internal sealed record CustomerPage(Customer[] Customers, int Skip, int Take, int Total);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sales_creates_a_customer_with_the_next_customer_number()
    {
        using var sales = api.CreateClientFor(Guid.NewGuid(), "Sales");

        var first = await sales.PostAsJsonAsync(Url, NewCustomer(PibA), Ct);
        var second = await sales.PostAsJsonAsync(Url, NewCustomer(PibB, name: "Beta d.o.o."), Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.ETag.ShouldNotBeNull();
        var created = await first.Content.ReadFromJsonAsync<Customer>(Ct);
        created.ShouldNotBeNull();
        created.ShouldBe(new Customer(created.Id, "CUST-00001", "Alfa d.o.o.", PibA, "07023014", "Bulevar 1, Beograd", "office@alfa.test", false));
        first.Headers.Location!.ToString().ShouldBe($"{Url}/{created.Id}");

        (await second.Content.ReadFromJsonAsync<Customer>(Ct))!.No.ShouldBe("CUST-00002");
        (await sales.GetFromJsonAsync<Customer>($"{Url}/{created.Id}", Ct)).ShouldBe(created);
    }

    [Theory]
    [InlineData("100002887")]
    [InlineData("104052135")]
    [InlineData("100001159")]
    [InlineData("101670560")]
    public async Task BR15_valid_pib_is_accepted(string pib)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PostAsJsonAsync(Url, NewCustomer(pib), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("100002888")]   // last digit changed: check digit no longer matches
    [InlineData("100002878")]   // two neighbouring digits swapped
    [InlineData("10000288")]    // 8 digits
    [InlineData("1000028870")]  // 10 digits
    [InlineData("10000288A")]   // not a digit
    [InlineData("")]
    public async Task BR15_invalid_pib_answers_400(string pib)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PostAsJsonAsync(Url, NewCustomer(pib), Ct);

        await ShouldBeValidationErrorAsync(response, "taxId");
    }

    [Theory]
    [InlineData("0702301")]
    [InlineData("070230145")]
    [InlineData("0702301A")]
    public async Task BR15_registration_number_must_be_8_digits(string registrationNo)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PostAsJsonAsync(Url, NewCustomer(PibA, registrationNo: registrationNo), Ct);

        await ShouldBeValidationErrorAsync(response, "registrationNo");
    }

    public static TheoryData<string, object> InvalidCustomers => new()
    {
        { "name", new { name = " ", taxId = PibA, registrationNo = "07023014", address = "A" } },
        { "address", new { name = "X", taxId = PibA, registrationNo = "07023014" } },
        { "email", new { name = "X", taxId = PibA, registrationNo = "07023014", address = "A", email = "not-an-email" } },
    };

    [Theory]
    [MemberData(nameof(InvalidCustomers))]
    public async Task Invalid_customer_answers_400_naming_the_field(string field, object body)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PostAsJsonAsync(Url, body, Ct);

        await ShouldBeValidationErrorAsync(response, field);
    }

    [Fact]
    public async Task Duplicate_pib_in_the_same_tenant_answers_409()
    {
        var tenant = Guid.NewGuid();
        await CreateAsync(tenant, PibA);
        using var sales = api.CreateClientFor(tenant, "Sales");

        var response = await sales.PostAsJsonAsync(Url, NewCustomer(PibA, name: "Someone else"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("sales.duplicate_tax_id");
    }

    [Fact]
    public async Task BR13_the_same_pib_in_another_tenant_is_allowed_and_numbered_from_1()
    {
        await CreateAsync(Guid.NewGuid(), PibA);
        await CreateAsync(Guid.NewGuid(), PibB);

        using var other = api.CreateClientFor(Guid.NewGuid(), "Sales");
        var response = await other.PostAsJsonAsync(Url, NewCustomer(PibA), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<Customer>(Ct))!.No.ShouldBe("CUST-00001");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    [InlineData("Finance")]
    public async Task Every_role_can_read_customers(string role)
    {
        var tenant = Guid.NewGuid();
        var customer = await CreateAsync(tenant, PibA);
        using var client = api.CreateClientFor(tenant, role);

        (await client.GetAsync(Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"{Url}/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Warehouse")]
    [InlineData("Finance")]
    public async Task Roles_without_customers_write_cannot_change_customers(string role)
    {
        var tenant = Guid.NewGuid();
        var (customer, etag) = await CreateWithETagAsync(tenant, PibA);
        using var client = api.CreateClientFor(tenant, role);

        (await client.PostAsJsonAsync(Url, NewCustomer(PibB), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await PutAsync(client, customer.Id, etag, NewCustomer(PibA, name: "Changed"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync($"{Url}/{customer.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_with_the_current_etag_changes_details_but_never_the_number()
    {
        var tenant = Guid.NewGuid();
        var (customer, etag) = await CreateWithETagAsync(tenant, PibA);
        using var sales = api.CreateClientFor(tenant, "Sales");

        var response = await PutAsync(sales, customer.Id, etag,
            new { name = "Alfa Group d.o.o.", taxId = PibB, registrationNo = "17123456", address = "Nova 2, Novi Sad", email = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldNotBe(etag);
        var updated = await response.Content.ReadFromJsonAsync<Customer>(Ct);
        updated.ShouldBe(customer with
        {
            Name = "Alfa Group d.o.o.", TaxId = PibB, RegistrationNo = "17123456", Address = "Nova 2, Novi Sad", Email = null,
        });
    }

    [Fact]
    public async Task Update_with_a_stale_etag_answers_412_and_without_one_428()
    {
        var tenant = Guid.NewGuid();
        var (customer, staleETag) = await CreateWithETagAsync(tenant, PibA);
        using var sales = api.CreateClientFor(tenant, "Sales");

        (await PutAsync(sales, customer.Id, staleETag, NewCustomer(PibA, name: "First change"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PutAsync(sales, customer.Id, staleETag, NewCustomer(PibA, name: "Lost update")))
            .StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await sales.PutAsJsonAsync($"{Url}/{customer.Id}", NewCustomer(PibA), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);
        (await sales.GetFromJsonAsync<Customer>($"{Url}/{customer.Id}", Ct))!.Name.ShouldBe("First change");
    }

    [Fact]
    public async Task Changing_the_pib_to_one_another_customer_has_answers_409()
    {
        var tenant = Guid.NewGuid();
        await CreateAsync(tenant, PibA);
        var (second, etag) = await CreateWithETagAsync(tenant, PibB);
        using var sales = api.CreateClientFor(tenant, "Sales");

        var response = await PutAsync(sales, second.Id, etag, NewCustomer(PibA));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BR11_block_and_unblock_change_the_flag()
    {
        var tenant = Guid.NewGuid();
        var customer = await CreateAsync(tenant, PibA);
        using var sales = api.CreateClientFor(tenant, "Sales");

        (await sales.PostAsync($"{Url}/{customer.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await sales.GetFromJsonAsync<Customer>($"{Url}/{customer.Id}", Ct))!.IsBlocked.ShouldBeTrue();

        (await sales.PostAsync($"{Url}/{customer.Id}/unblock", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await sales.GetFromJsonAsync<Customer>($"{Url}/{customer.Id}", Ct))!.IsBlocked.ShouldBeFalse();
    }

    [Fact]
    public async Task Customers_cannot_be_deleted()
    {
        var tenant = Guid.NewGuid();
        var customer = await CreateAsync(tenant, PibA);
        using var admin = api.CreateClientFor(tenant, "Admin");

        (await admin.DeleteAsync($"{Url}/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task List_is_ordered_by_number_and_searchable_by_name_or_pib()
    {
        var tenant = Guid.NewGuid();
        await CreateAsync(tenant, PibA, name: "Alfa d.o.o.");
        await CreateAsync(tenant, PibB, name: "Beta a.d.");
        using var finance = api.CreateClientFor(tenant, "Finance");

        var all = await finance.GetFromJsonAsync<CustomerPage>(Url, Ct);
        all!.Customers.Select(c => c.No).ShouldBe(["CUST-00001", "CUST-00002"]);

        (await finance.GetFromJsonAsync<CustomerPage>($"{Url}?search=beta", Ct))!
            .Customers.ShouldHaveSingleItem().TaxId.ShouldBe(PibB);
        (await finance.GetFromJsonAsync<CustomerPage>($"{Url}?search={PibA}", Ct))!
            .Customers.ShouldHaveSingleItem().Name.ShouldBe("Alfa d.o.o.");
    }

    [Fact]
    public async Task BR13_another_tenants_customer_is_not_found_on_any_endpoint()
    {
        var owner = Guid.NewGuid();
        var (customer, etag) = await CreateWithETagAsync(owner, PibA);
        using var intruder = api.CreateClientFor(Guid.NewGuid(), "Admin");

        (await intruder.GetAsync($"{Url}/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PutAsync(intruder, customer.Id, etag, NewCustomer(PibA, name: "Hijacked"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostAsync($"{Url}/{customer.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostAsync($"{Url}/{customer.Id}/unblock", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var list = await intruder.GetFromJsonAsync<CustomerPage>($"{Url}?search={PibA}", Ct);
        list!.Customers.ShouldBeEmpty();
        list.Total.ShouldBe(0);

        using var ownerClient = api.CreateClientFor(owner, "Admin");
        (await ownerClient.GetFromJsonAsync<Customer>($"{Url}/{customer.Id}", Ct)).ShouldBe(customer);
    }

    private static object NewCustomer(
        string taxId,
        string name = "Alfa d.o.o.",
        string registrationNo = "07023014",
        string address = "Bulevar 1, Beograd",
        string? email = "office@alfa.test") =>
        new { name, taxId, registrationNo, address, email };

    private async Task<Customer> CreateAsync(Guid tenant, string taxId, string name = "Alfa d.o.o.") =>
        (await CreateWithETagAsync(tenant, taxId, name)).Customer;

    private async Task<(Customer Customer, string ETag)> CreateWithETagAsync(Guid tenant, string taxId, string name = "Alfa d.o.o.")
    {
        using var admin = api.CreateClientFor(tenant, "Admin");
        var response = await admin.PostAsJsonAsync(Url, NewCustomer(taxId, name), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return ((await response.Content.ReadFromJsonAsync<Customer>(Ct))!, response.Headers.ETag!.Tag);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{Url}/{id}") { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(etag));
        return client.SendAsync(request, Ct);
    }

    private static async Task ShouldBeValidationErrorAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue($"expected an error for '{field}'");
    }
}
