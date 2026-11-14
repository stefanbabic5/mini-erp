using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace MiniErp.IntegrationTests.Catalog;

/// <summary>
/// <c>/api/catalog/items</c>: create, read, list, update with optimistic concurrency, block.
/// Every test uses fresh tenants, so they never see each other's items.
/// </summary>
public class ItemEndpointTests(ApiFactory api)
{
    private const string Url = "/api/catalog/items";

    internal sealed record Item(
        Guid Id, string Sku, string Name, decimal UnitPrice, string VatRate, int VatPercent, bool IsBlocked);

    internal sealed record ItemPage(Item[] Items, int Skip, int Take, int Total);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Warehouse_creates_an_item_and_reads_it_back()
    {
        using var warehouse = api.CreateClientFor(Guid.NewGuid(), "Warehouse");

        var created = await warehouse.PostAsJsonAsync(Url, NewItem("BOLT-M8", unitPrice: 12.50m), Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.ETag.ShouldNotBeNull();
        var item = await created.Content.ReadFromJsonAsync<Item>(Ct);
        item.ShouldNotBeNull();
        created.Headers.Location!.ToString().ShouldBe($"{Url}/{item.Id}");
        item.ShouldBe(new Item(item.Id, "BOLT-M8", "Bolt M8", 12.50m, "Standard", 20, IsBlocked: false));

        var read = await warehouse.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct);
        read.ShouldBe(item);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    [InlineData("Finance")]
    public async Task Every_role_can_read_the_catalog(string role)
    {
        var tenant = Guid.NewGuid();
        var item = await CreateAsync(tenant, "READ-1");
        using var client = api.CreateClientFor(tenant, role);

        (await client.GetAsync(Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"{Url}/{item.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Finance")]
    public async Task Roles_without_catalog_write_cannot_change_items(string role)
    {
        var tenant = Guid.NewGuid();
        var (item, etag) = await CreateWithETagAsync(tenant, "LOCKED-1");
        using var client = api.CreateClientFor(tenant, role);

        (await client.PostAsJsonAsync(Url, NewItem("NEW-1"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await PutAsync(client, item.Id, etag, NewUpdate())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync($"{Url}/{item.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_sku_in_the_same_tenant_answers_409()
    {
        var tenant = Guid.NewGuid();
        await CreateAsync(tenant, "DUP-1");
        using var admin = api.CreateClientFor(tenant, "Admin");

        var response = await admin.PostAsJsonAsync(Url, NewItem("DUP-1"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("catalog.duplicate_sku");
    }

    [Fact]
    public async Task BR13_the_same_sku_in_another_tenant_is_allowed()
    {
        await CreateAsync(Guid.NewGuid(), "SHARED-1");

        using var other = api.CreateClientFor(Guid.NewGuid(), "Admin");
        var response = await other.PostAsJsonAsync(Url, NewItem("SHARED-1"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public static TheoryData<string, object> InvalidItems => new()
    {
        { "sku", new { sku = "lower-case", name = "X", unitPrice = 1m, vatRate = "Standard" } },
        { "sku", new { sku = "", name = "X", unitPrice = 1m, vatRate = "Standard" } },
        { "sku", new { sku = "THIS-SKU-IS-FAR-TOO-LONG", name = "X", unitPrice = 1m, vatRate = "Standard" } },
        { "name", new { sku = "OK-1", name = " ", unitPrice = 1m, vatRate = "Standard" } },
        { "unitPrice", new { sku = "OK-1", name = "X", unitPrice = -1m, vatRate = "Standard" } },
        { "unitPrice", new { sku = "OK-1", name = "X", unitPrice = 1.005m, vatRate = "Standard" } },
        { "unitPrice", new { sku = "OK-1", name = "X", vatRate = "Standard" } },
        { "vatRate", new { sku = "OK-1", name = "X", unitPrice = 1m } },
    };

    [Theory]
    [MemberData(nameof(InvalidItems))]
    public async Task Invalid_item_answers_400_naming_the_field(string field, object body)
    {
        using var admin = api.CreateClientFor(Guid.NewGuid(), "Admin");

        var response = await admin.PostAsJsonAsync(Url, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Update_with_the_current_etag_changes_the_item_and_its_etag()
    {
        var tenant = Guid.NewGuid();
        var (item, etag) = await CreateWithETagAsync(tenant, "UPD-1");
        using var warehouse = api.CreateClientFor(tenant, "Warehouse");

        var response = await PutAsync(warehouse, item.Id, etag, new { name = "Renamed", unitPrice = 99.99m, vatRate = "Reduced" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldNotBe(etag);
        var updated = await response.Content.ReadFromJsonAsync<Item>(Ct);
        updated.ShouldBe(item with { Name = "Renamed", UnitPrice = 99.99m, VatRate = "Reduced", VatPercent = 10 });
    }

    [Fact]
    public async Task Update_with_a_stale_etag_answers_412_and_keeps_the_other_change()
    {
        var tenant = Guid.NewGuid();
        var (item, staleETag) = await CreateWithETagAsync(tenant, "RACE-1");
        using var warehouse = api.CreateClientFor(tenant, "Warehouse");

        // Someone else changes the item first...
        (await PutAsync(warehouse, item.Id, staleETag, new { name = "First change", unitPrice = 1m, vatRate = "Standard" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...so a change based on the old version must not overwrite theirs.
        var response = await PutAsync(warehouse, item.Id, staleETag, new { name = "Lost update", unitPrice = 2m, vatRate = "Standard" });

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await warehouse.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct))!.Name.ShouldBe("First change");
    }

    [Fact]
    public async Task Update_without_if_match_answers_428()
    {
        var tenant = Guid.NewGuid();
        var item = await CreateAsync(tenant, "NOMATCH-1");
        using var warehouse = api.CreateClientFor(tenant, "Warehouse");

        var response = await warehouse.PutAsJsonAsync($"{Url}/{item.Id}", NewUpdate(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task BR11_block_and_unblock_change_the_flag()
    {
        var tenant = Guid.NewGuid();
        var item = await CreateAsync(tenant, "BLOCK-1");
        using var warehouse = api.CreateClientFor(tenant, "Warehouse");

        (await warehouse.PostAsync($"{Url}/{item.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await warehouse.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct))!.IsBlocked.ShouldBeTrue();

        (await warehouse.PostAsync($"{Url}/{item.Id}/unblock", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await warehouse.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct))!.IsBlocked.ShouldBeFalse();
    }

    [Fact]
    public async Task BR12_items_cannot_be_deleted()
    {
        var tenant = Guid.NewGuid();
        var item = await CreateAsync(tenant, "KEEP-1");
        using var admin = api.CreateClientFor(tenant, "Admin");

        var response = await admin.DeleteAsync($"{Url}/{item.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task List_is_ordered_by_sku_searchable_and_paged()
    {
        var tenant = Guid.NewGuid();
        foreach (var sku in new[] { "C-3", "A-1", "B-2" })
        {
            await CreateAsync(tenant, sku, name: $"Product {sku}");
        }

        using var sales = api.CreateClientFor(tenant, "Sales");

        var all = await sales.GetFromJsonAsync<ItemPage>(Url, Ct);
        all!.Items.Select(i => i.Sku).ShouldBe(["A-1", "B-2", "C-3"]);
        all.Total.ShouldBe(3);

        var second = await sales.GetFromJsonAsync<ItemPage>($"{Url}?skip=1&take=1", Ct);
        second!.Items.ShouldHaveSingleItem().Sku.ShouldBe("B-2");
        second.Total.ShouldBe(3);

        var search = await sales.GetFromJsonAsync<ItemPage>($"{Url}?search=product c", Ct);
        search!.Items.ShouldHaveSingleItem().Sku.ShouldBe("C-3");
    }

    [Fact]
    public async Task BR13_another_tenants_item_is_not_found_on_any_endpoint()
    {
        var owner = Guid.NewGuid();
        var (item, etag) = await CreateWithETagAsync(owner, "SECRET-1");
        using var intruder = api.CreateClientFor(Guid.NewGuid(), "Admin");

        (await intruder.GetAsync($"{Url}/{item.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PutAsync(intruder, item.Id, etag, NewUpdate())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostAsync($"{Url}/{item.Id}/block", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostAsync($"{Url}/{item.Id}/unblock", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var list = await intruder.GetFromJsonAsync<ItemPage>($"{Url}?search=SECRET", Ct);
        list!.Items.ShouldBeEmpty();
        list.Total.ShouldBe(0);

        // The owner's item is untouched.
        using var ownerClient = api.CreateClientFor(owner, "Admin");
        var stillThere = await ownerClient.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct);
        stillThere.ShouldBe(item);
    }

    private static object NewItem(string sku, string name = "Bolt M8", decimal unitPrice = 10m, string vatRate = "Standard") =>
        new { sku, name, unitPrice, vatRate };

    private static object NewUpdate() => new { name = "Changed", unitPrice = 5m, vatRate = "Exempt" };

    private async Task<Item> CreateAsync(Guid tenant, string sku, string name = "Bolt M8") =>
        (await CreateWithETagAsync(tenant, sku, name)).Item;

    private async Task<(Item Item, string ETag)> CreateWithETagAsync(Guid tenant, string sku, string name = "Bolt M8")
    {
        using var admin = api.CreateClientFor(tenant, "Admin");
        var response = await admin.PostAsJsonAsync(Url, NewItem(sku, name), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<Item>(Ct);
        return (item!, response.Headers.ETag!.Tag);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{Url}/{id}") { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(etag));
        return client.SendAsync(request, Ct);
    }
}
