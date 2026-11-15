---
name: mini-erp-testing-and-qa
description: >
  The test suite of mini-erp and what counts as evidence in this project. Load this
  when writing or changing any test; when deciding which tests a new endpoint,
  entity, document with a lifecycle, permission, business rule (BR-xx) or Platform
  change needs; when a test fails and you need to know what it protects; when using
  ApiFactory, TestTokens, TenantScope, Testcontainers, PermissionMatrixTests or the
  architecture tests' Modules.cs; when writing a cross-tenant (BR-13) test, an ETag /
  If-Match test, a validation theory or a parallel concurrency test (BR-02, BR-09);
  when asked "is this proven", "which test covers BR-xx", "how many tests are
  there"; and before reporting test results. Covers the two test projects, the
  fixtures, naming rules, the required test set with code templates, the standard
  for a concurrency proof, and which rules have a test today. Commands to run tests:
  mini-erp-build-and-run. The planted-bug procedure and PR reporting:
  mini-erp-change-control.
---

# mini-erp testing and QA

What a test must look like in **mini-erp**, and what may be called "proven". The
governing rule is working agreement 4 in `CLAUDE.md`: **evidence over claims**. Nothing
works until a run shows it; results are reported as they are; the database is never
mocked.

State on 2026-11-15: `dotnet test` runs **121** tests (9 architecture, 112
integration), all green, in about 16 seconds with Docker running. Trust the output of
a fresh run over this number.

Terms, defined once:

- **Integration test**: the real API hosted in memory (`WebApplicationFactory`), talking
  to a real PostgreSQL that Testcontainers starts in Docker.
- **Fixture**: an object the test framework creates once and hands to test classes
  (AL: the shared initialisation of a test codeunit).
- **DI scope**: one set of per-request services (a DbContext, the tenant context). The
  API creates one per HTTP request.
- **`[Fact]`**: one test (AL: a `[Test]` procedure; Shouldly plays the role of Library
  Assert). **`[Theory]`**: one test method run once per data row
  (`[InlineData]` or `[MemberData]`); each row counts as a test in the run total.
- **Cross-tenant test**: a test where tenant B tries to reach tenant A's data (BR-13).
- **Planted-bug check**: breaking the code on purpose to see the tests fail
  (mini-erp-change-control §6).

## 1. The two test projects

| Project | What it checks | Needs Docker |
|---|---|---|
| `tests/MiniErp.ArchitectureTests` | module boundaries (ADR-0001) by reflection over the compiled assemblies: `ModuleBoundaryTests`, with the module list in `Modules.cs` | no |
| `tests/MiniErp.IntegrationTests` | everything else, through HTTP or through the real DI container | yes |

There is **no unit test project** (specification §9 asks for unit tests on the domain;
that is an open gap, not a convention to copy). Until one exists, pure rules such as the
PIB check digit are tested through the endpoint that uses them.

Stack: xUnit v3 on Microsoft.Testing.Platform, Shouldly for assertions. Not allowed:
FluentAssertions v8+ (commercial licence, ADR-0007), an in-memory or mocked database.

Run commands and filters: mini-erp-build-and-run §5.

## 2. The fixtures

All three are in `tests/MiniErp.IntegrationTests/`.

| Type | Use it for | Key members |
|---|---|---|
| `ApiFactory` | every integration test; received through the class's primary constructor | `CreateClientFor(tenantId, roles...)`, `CreateClientWithToken(token)`, `CreateClient()` (no token), `Services` |
| `TestTokens` | tokens shaped like Keycloak's, signed with a key only the test host trusts | `For(tenantId, roles...)`, `Create(...)` for negative cases (no tenant, other audience, expired, untrusted key), `Principal(tenantId, roles...)`, `TenantAlfa`, `TenantBeta` |
| `TenantScope` | tests that call a service or a DbContext directly instead of HTTP | `await using var scope = TenantScope.For(api, tenantId);` then `scope.Get<T>()` |

How they work, so failures make sense:

- `ApiFactory` is an **assembly fixture**: one PostgreSQL container
  (`postgres:18.7-alpine`) and one API host for the whole run. The host runs in the
  `Development` environment, so all module migrations are applied at start.
- All tests share that one database. Isolation between tests comes from **tenants**:
  every test uses `Guid.NewGuid()` as its tenant, so it never sees another test's rows.
- Keycloak is never contacted. The API validates tokens exactly as in production, only
  against the test issuer and key.
- `TenantScope` creates a DI scope and puts a fake `HttpContext` with the tenant's
  principal into `IHttpContextAccessor`, so `ITenantContext` works as in a request.
  Each scope has its own DbContext and connection.

## 3. Conventions

- One test class per feature, at `tests/MiniErp.IntegrationTests/<Module>/<Entity>EndpointTests.cs`;
  Platform tests sit in the project root.
- `public class XTests(ApiFactory api)`: no base class, no shared mutable state.
- **A fresh tenant per test**: `var tenant = Guid.NewGuid();`. Use `TestTokens.TenantAlfa`
  only where data is not written.
- Roles are the strings `"Admin"`, `"Sales"`, `"Warehouse"`, `"Finance"`.
- Test names are sentences in snake_case that state the behaviour:
  `Update_with_a_stale_etag_answers_412_and_keeps_the_other_change`.
- **A test that proves a business rule starts with the rule id without the dash**:
  `BR13_...`, `BR15_...`. The id comes from specification §6. Do not put an id on a test
  that does not prove that rule.
- Every async call gets `TestContext.Current.CancellationToken` (the two endpoint test
  classes shorten it with
  `private static CancellationToken Ct => TestContext.Current.CancellationToken;`).
- Response shapes are records **declared inside the test class** (`internal` or
  `private`), not the module's internal types. The test then checks the JSON contract a client sees (enum as
  `"Standard"`, property names), and modules stay closed to the test project.
- Request bodies are anonymous objects (`new { sku, name, unitPrice, vatRate }`), which
  also lets a test omit a field.
- Small private helpers at the bottom of the class: `NewItem(...)`, `CreateAsync(...)`,
  `CreateWithETagAsync(...)`, `PutAsync(client, id, etag, body)`.
- Assertions with Shouldly: `ShouldBe`, `ShouldBeEmpty`, `ShouldHaveSingleItem`,
  `ShouldBeUnique`, `Should.ThrowAsync<T>`.

## 4. The required test set for a feature

For an entity with the standard endpoints (list, get, create, update, actions), write
at least these. `Catalog/ItemEndpointTests.cs` is the reference; names below are from
it.

| # | Proves | Reference test |
|---|---|---|
| 1 | create returns 201, `Location`, `ETag`, the body; reading it back gives the same | `Warehouse_creates_an_item_and_reads_it_back` |
| 2 | every role that should read, can (theory over roles) | `Every_role_can_read_the_catalog` |
| 3 | roles without the write permission get **403 on the write endpoints** (the reference tries create, update and block; include unblock and every new action) | `Roles_without_catalog_write_cannot_change_items` |
| 4 | each business conflict answers 409 with its `code` | `Duplicate_sku_in_the_same_tenant_answers_409` |
| 5 | uniqueness is per tenant: the same value in another tenant is allowed | `BR13_the_same_sku_in_another_tenant_is_allowed` |
| 6 | each validation rule answers 400 naming the field (one `[MemberData]` theory) | `Invalid_item_answers_400_naming_the_field` |
| 7 | update with the current ETag succeeds and returns a new ETag | `Update_with_the_current_etag_changes_the_item_and_its_etag` |
| 8 | update with a stale ETag answers 412 **and the first change is still there** | `Update_with_a_stale_etag_answers_412_and_keeps_the_other_change` |
| 9 | update without `If-Match` answers 428 | `Update_without_if_match_answers_428` |
| 10 | each action changes what it should | `BR11_block_and_unblock_change_the_flag` |
| 11 | `DELETE` answers 405 | `BR12_items_cannot_be_deleted` |
| 12 | list ordering, paging and search | `List_is_ordered_by_sku_searchable_and_paged` |
| 13 | **the cross-tenant test** | `BR13_another_tenants_item_is_not_found_on_any_endpoint` |

A new permission also needs its rows in `PermissionMatrixTests.Matrix` (all four
places: mini-erp-add-feature §7). A new module needs an entry in
`tests/MiniErp.ArchitectureTests/Modules.cs` (`Modules.All`) and a project reference in
that test project.

### A document with a lifecycle (phase 2: sales orders)

The table above is shaped by master data. No such test exists yet; these rows come
from specification §5 and §6 and are what a sales order feature must add:

| Proves | Rule |
|---|---|
| each allowed transition changes the status and has its side effect (release reserves, reopen and cancel remove reservations) | specification §5 |
| each refused transition answers 409 with a `code` and changes nothing (reopen after a shipment, cancel after a shipment) | specification §5 |
| a released order's lines cannot be edited | BR-07 |
| price, VAT rate, item name and customer data on the document do not change when the catalog or customer changes later | BR-08 |
| a blocked item cannot be added; a blocked customer gets no new order or release | BR-11 |
| a release that lacks stock on any line reserves nothing and names the short lines | specification §5 |
| the parallel release test and the ledger reconciliation | BR-02, §5 below |
| another tenant's ids in the route, **and in the request body** (`customerId`, `itemId`, `warehouseId`) | BR-13; the status code for a body id is an open question (mini-erp-architecture-contract §6): ask Stefan, then test it |

### Templates for the validation theory and the stale ETag

Condensed from `ItemEndpointTests.cs`; `Url` is the class's `private const string Url`,
`NewUpdate()`, `CreateWithETagAsync` and `PutAsync` are its private helpers, `Item` and
`ItemPage` its response records.

```csharp
public static TheoryData<string, object> InvalidItems => new()
{
    { "sku", new { sku = "lower-case", name = "X", unitPrice = 1m, vatRate = "Standard" } },
    { "unitPrice", new { sku = "OK-1", name = "X", vatRate = "Standard" } },   // field left out
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
public async Task Update_with_a_stale_etag_answers_412_and_keeps_the_other_change()
{
    var tenant = Guid.NewGuid();
    var (item, staleETag) = await CreateWithETagAsync(tenant, "RACE-1");
    using var warehouse = api.CreateClientFor(tenant, "Warehouse");

    (await PutAsync(warehouse, item.Id, staleETag, new { name = "First change", unitPrice = 1m, vatRate = "Standard" }))
        .StatusCode.ShouldBe(HttpStatusCode.OK);

    var response = await PutAsync(warehouse, item.Id, staleETag, new { name = "Lost update", unitPrice = 2m, vatRate = "Standard" });

    response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    (await warehouse.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct))!.Name.ShouldBe("First change");
}
```

The second assertion of the ETag test is the point: the first change is still there.

### The cross-tenant test (BR-13)

One per tenant-scoped entity. It must try **every** endpoint that takes an id, the
list, and then check the owner's data is untouched (condensed from the reference):

```csharp
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

    using var ownerClient = api.CreateClientFor(owner, "Admin");
    (await ownerClient.GetFromJsonAsync<Item>($"{Url}/{item.Id}", Ct)).ShouldBe(item);
}
```

Rules: the intruder is an `Admin` (so a 404 cannot be a hidden 403); the intruder sends
a **valid** ETag on `PUT` (so the 404 is not a 412 or 428); **when you add an endpoint
to the entity, add a line here.**

### A test below HTTP

Use `TenantScope` when the behaviour has no endpoint or when you must reach past the
API (as `TenantIsolationTests` does to prove the write guard):

```csharp
await using var scope = TenantScope.For(api, Guid.NewGuid());
var series = scope.Get<INumberSeriesService>();
var first = await series.NextAsync(NumberSeriesCodes.Customer, TestContext.Current.CancellationToken);
first.ShouldBe("CUST-00001");
```

Only Platform's internals are visible to the test project (`InternalsVisibleTo` in
`MiniErp.Platform.csproj`). A module's internal types are not; do not add
`InternalsVisibleTo` to a module without asking Stefan.

## 5. The standard for a concurrency proof

"It is correct under concurrency" may be said only when a **parallel test on real
PostgreSQL** shows it (working agreement 4; BR-02 and BR-09 are named explicitly).
Reasoning, a code review or a single-threaded test is not proof.

The existing example, `NumberSeriesTests.BR09_parallel_requests_never_get_the_same_number`
(condensed; `Task.WhenAll` starts all tasks and waits for every one):

```csharp
const int requests = 50;
var tenant = Guid.NewGuid();

var numbers = await Task.WhenAll(Enumerable.Range(0, requests).Select(async _ =>
{
    await using var scope = TenantScope.For(api, tenant);   // own scope = own connection
    return await scope.Get<INumberSeriesService>().NextAsync(NumberSeriesCodes.Shipment, Ct);
}));

numbers.ShouldBeUnique();
numbers.Order(StringComparer.Ordinal).ShouldBe(Enumerable.Range(1, requests).Select(n => $"SHIP-{n:D5}"));
```

What makes it a proof, and what a new parallel test must also do:

1. **Real parallelism**: every task has its own scope (or its own `HttpClient` call),
   so each uses its own database connection. One shared DbContext would serialise the
   work and prove nothing.
2. **All tasks hit the same contended row** (same tenant, same series).
3. **The assertion is the invariant, exactly**: not "no exception", but the precise set
   of results (here: 50 distinct numbers that are exactly 1..50).
4. **A planted bug makes it fail.** If replacing the atomic statement with
   read-then-write does not turn the test red, the test is not exercising the race.
5. Enough contention to fail reliably when the code is wrong (50 here). A test that
   fails one run in ten is a defect in the test.

For phase 2 (BR-02), specification §8 and ADR-0008 already fix what the proof must
show: N parallel releases for more units than exist, exactly the stock-covered ones
succeed, `Available` is never negative, and the ledger sum equals the balances. That
test does not exist yet. Two things about writing it:

- It must go **through HTTP**, because a module's internals are not visible to the
  test project: `Task.WhenAll` over N calls, each with its own
  `api.CreateClientFor(tenant, "Sales")` posting a release, then assert the exact
  number of successes and of 409 answers with `inventory.insufficient_stock`.
- The reconciliation half must read the ledger and the balances. Whether through an
  endpoint or through raw SQL with an explicit tenant condition is an open question
  (mini-erp-architecture-contract §6).

## 6. Which rules have a proof today

From the test names on 2026-11-15 (`dotnet test --no-build --list-tests`):

| Rule | Proven by | Limit of the proof |
|---|---|---|
| BR-09 | `BR09_parallel_requests_never_get_the_same_number` | only the "unique, gaps allowed" half; gapless invoice numbers are not built |
| BR-11 | `BR11_block_and_unblock_change_the_flag` (items, customers) | only the flag; "a blocked item cannot be added to an order" needs orders (phase 2) |
| BR-12 | `BR12_items_cannot_be_deleted` | items; warehouses do not exist yet |
| BR-13 | 11 tests: data layer (`TenantIsolationTests`), items, customers, number series | per feature; a new entity is unproven until its own test exists |
| BR-15 | `BR15_valid_pib_is_accepted`, `BR15_invalid_pib_answers_400`, `BR15_registration_number_must_be_8_digits` | complete |
| BR-10 | the 2-decimal price rule is tested inside `Invalid_item_answers_400_naming_the_field`, without the id in a name | rounding and per-line VAT need documents (phase 2-3) |
| BR-01 to BR-08, BR-14, BR-16 | nothing | not built |

What each phase 2 rule will need as proof (from specification §6 and §8, ADR-0004,
ADR-0008; none of it exists):

| Rule | Proof it needs |
|---|---|
| BR-01 ledger never updated or deleted | a test that an `UPDATE` or `DELETE` on the ledger is refused by the database (ADR-0004's restricted role), not only that no endpoint does it |
| BR-02 `Available` never negative | the parallel release test (§5) |
| BR-03 `OnHand` never negative | a refused negative adjustment; a parallel variant if adjustments can race |
| BR-04 shipped ≤ reserved | a refused over-shipment |
| BR-07, BR-08, BR-11 | the lifecycle table in §4 |
| BR-12 for warehouses | 405 on `DELETE`, block instead |
| BR-14 idempotency | the same key and body returns the original response without repeating the effect; the same key with another body answers 422 |
| BR-16 back order and close remainder | a partial shipment keeps the rest reserved; closing releases it and cannot be undone |
| ledger and balance reconcile | a test that sums the ledger and compares it with every balance row |

Which test protects which architectural invariant (tenancy, security, boundaries): the
table in mini-erp-architecture-contract §2.

Also proven without a BR id: the permission matrix (`PermissionMatrixTests`), token
validation (`AuthenticationTests`), lost-update protection (the ETag tests), health
probes and Problem Details (`HostingTests`), module boundaries (`ModuleBoundaryTests`).

## 7. Reporting results

- Report the numbers from the run you just did: "`dotnet test`: 121 passed, 0 failed".
  If something failed, say which tests and paste the assertion message.
- "Passed locally" is not "CI is green". CI runs the Release configuration on Linux;
  check with `gh pr checks <number>` after pushing.
- Never weaken, skip or delete a test to get to green. A test that is wrong is fixed in
  the open, with the reason in the PR.
- Do the planted-bug check for new behaviour and report it in one line.
- Counts in `README.md` change only after a run that shows the new count
  (mini-erp-claims-and-evidence).

## 8. Traps

| Trap | Why it bites | Do |
|---|---|---|
| Reusing a tenant id across tests | all tests share one database; counts, searches and number series collide | `Guid.NewGuid()` per test |
| Asserting `CUST-00001` with `TestTokens.TenantAlfa` | another test may have advanced Alfa's series | fresh tenant |
| Checking 404 with a role that lacks the permission | the request answers 403 before reaching the data; the test proves nothing about tenancy | the intruder is `Admin` |
| `PUT` without `If-Match` in a cross-tenant test | fine today (load comes first, so it is 404), but the test then also depends on handler order | send a valid ETag |
| Deserialising into the module's internal records | does not compile; and it would hide contract changes | private records in the test class |
| The `--list-tests` count taken as the number of tests | it lists 112 while a run reports 121: the two validation theories whose rows are anonymous objects are listed once | quote the run total |
| Docker stopped | every integration test fails in the fixture, with a container error rather than an assertion | start Docker; mini-erp-build-and-run §7 |
| Parallel tasks sharing one scope or DbContext | no real concurrency; a DbContext is also not thread-safe | one scope per task |
| A test that passes with the feature removed | it asserts too little (status code only) | planted-bug check |

## When NOT to use this skill

| You need | Use |
|---|---|
| The commands and filters to run tests, Docker problems | mini-erp-build-and-run |
| How to write the feature under test | mini-erp-add-feature |
| What an invariant is and why it exists | mini-erp-architecture-contract |
| What to write in the PR, the planted-bug procedure | mini-erp-change-control |
| Whether a README or other document's sentence is backed by a test | mini-erp-claims-and-evidence |

## Provenance and maintenance

Written 2026-11-15 from `tests/`, PRs #3-#8 and a full green run that day. Re-verify
what may drift:

```bash
dotnet test                                                        # the real totals
dotnet test --no-build --list-tests | grep -oE "BR[0-9]+" | sort | uniq -c   # rule ids present in test names
ls tests/MiniErp.IntegrationTests tests/MiniErp.IntegrationTests/*/           # test classes
git grep -n "InternalsVisibleTo" -- src                            # expect only MiniErp.Platform.csproj
git grep -n "PostgreSqlBuilder" -- tests                           # image used by the tests
ls tests                                                           # a unit test project would change §1
```
