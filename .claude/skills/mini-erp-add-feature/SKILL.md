---
name: mini-erp-add-feature
description: >
  Step-by-step recipe for adding or changing code in mini-erp in the house style.
  Load this when adding a feature folder to a module (entity, request/response
  records, validation, endpoints); when adding, changing or fixing an endpoint, an
  action (POST /{id}/something), a validation rule, an entity or a column; when
  creating an EF Core migration or the first DbContext of a module (Inventory,
  Invoicing); when adding a permission, a number series or an error code; when
  starting phase 2 work such as warehouses, sales orders, order lines or the ledger;
  when copying src/Modules/MiniErp.Catalog/Items as a template; when dotnet ef
  migrations add or remove fails or produces an unexpected migration; and when a C#
  or EF Core idiom in the code needs explaining to someone from Business Central / AL
  (records, internal, async, LINQ). The rules
  behind the recipe: mini-erp-architecture-contract. The tests each change needs:
  mini-erp-testing-and-qa. The gates: mini-erp-change-control.
---

# Adding a feature to mini-erp

How to add code so that it looks like the code already there. The reference
implementation is **`src/Modules/MiniErp.Catalog/Items/`** (items); the second example,
built from the same recipe, is `src/Modules/MiniErp.Sales/Customers/`. Read the
reference files before writing; this skill tells you what to copy and what must
change. `CLAUDE.md` ("Endpoint conventions") is the short statement of the rules.

Explicit, readable code is preferred over clever code: the maintainer must be able to
explain every line in an interview (working agreement 7). No mediator and no mapper
library (ADR-0007). Handlers use the module's DbContext directly, as the reference
does; there is no repository layer to copy.

State on 2026-11-15: two features exist (items, customers). Everything about phase 2
below is a pointer to a decision or an open question, never a description of code.

Before starting, on a branch (never `main`; see mini-erp-change-control):

```bash
dotnet tool restore
```
```bash
dotnet build
```

## 1. The shape of a feature

One folder per feature inside its module. Everything is `internal`: visible only
inside the module's own project (AL: `Access = Internal`).

| File | Contains | In the reference |
|---|---|---|
| `<Entity>.cs` | the entity: state and the methods that change it | `Item.cs` |
| `<Entity>Contracts.cs` | request and response records, the page record | `ItemContracts.cs` |
| `<Entity>Validation.cs` | field rules returning `Dictionary<string, string[]>` | `ItemValidation.cs` (customers keep validation in `CustomerContracts.cs`) |
| `<Entity>Endpoints.cs` | `Map<Entity>Endpoints` and one private static handler per endpoint | `ItemEndpoints.cs` |

Plus, outside the folder: a `DbSet` and mapping in the module's `<Name>DbContext.cs`, a
call in `<Name>Module.Map<Name>Module`, a migration, and tests under
`tests/MiniErp.IntegrationTests/<Module>/`.

## 2. Order of work

1. **Specification first.** If the feature adds or changes a business rule, a
   permission, an entity field or an endpoint convention, edit `docs/specification.md`
   in the same change (§4 domain model, §6 rules, §2 permissions). A new rule gets the
   next free `BR-` id.
2. **Entity** (§3).
3. **Mapping and migration** (§4).
4. **Contracts and validation** (§5).
5. **Endpoints** (§6), then register them in the module class.
6. **Permission**, if a new one is needed (§7).
7. **Tests** (mini-erp-testing-and-qa): happy path, permissions, validation, concurrency,
   the cross-tenant test that tries every endpoint, rule ids in names.
8. `dotnet build`, `dotnet test`, the planted-bug check, then update `README.md`
   ("What works today") and `CLAUDE.md` if a convention or the layout changed.

## 3. Entity checklist

Copy the structure of `Item.cs`:

- `internal sealed class X : ITenantOwned`. **Without `ITenantOwned` the entity has no
  tenant filter and no write guard.**
- `public Guid TenantId { get; private set; }` and never assign it: the interceptor
  stamps it on insert.
- `Id = Guid.CreateVersion7();` in the public constructor (time-ordered GUIDs keep the
  primary key index compact).
- A private parameterless constructor for EF Core that sets non-nullable strings to
  `string.Empty`.
- Private setters; changes go through methods (`Update`, `Block`, `Unblock`) that also
  set `UpdatedAt = DateTimeOffset.UtcNow`.
- `public uint Version { get; private set; }` if the entity can be updated: it is the
  row version (`xmin`) and becomes the ETag.
- Length limits as `public const int Max...Length`, used by both the mapping and the
  validation.
- Money is `decimal`, 2 decimals (BR-10). Timestamps are `DateTimeOffset` in UTC.
- A doc comment naming the Business Central equivalent and the rules the entity carries.
- **A line or child entity is tenant-owned too** (an order line, a posted shipment
  line): it implements `ITenantOwned`, even where specification §4 lists its fields
  without `TenantId`. ADR-0003 puts a `TenantId` column on every tenant-scoped row,
  and only `ITenantOwned` gives a row its filter and write guard.
- Quantities: their type and precision are not decided (BR-10 covers money only). Ask
  Stefan before the first Inventory migration.

## 4. Mapping and migration

In `<Name>DbContext.ConfigureModel` add the `DbSet` and the mapping. Rules:

- `ToTable("<plural_snake_case>")`; column names become snake_case automatically.
- Every unique index starts with `TenantId`: `HasIndex(x => new { x.TenantId, x.Sku }).IsUnique()`.
- `HasMaxLength` on every string; `HasPrecision(18, 2)` on money.
- Enums are stored as text: `.HasConversion<string>().HasMaxLength(n)`.
- `.Property(x => x.Version).IsRowVersion()` maps `uint Version` to `xmin`.

Create the migration (Catalog shown; change project and context for another module):

```bash
dotnet ef migrations add <Name> --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext --output-dir Migrations
```

| Module | `--project` | `--context` |
|---|---|---|
| Catalog | `src/Modules/MiniErp.Catalog` | `CatalogDbContext` |
| Sales | `src/Modules/MiniErp.Sales` | `SalesDbContext` |
| Platform | `src/MiniErp.Platform` | `PlatformDbContext` |

Then:

1. **Read the generated migration.** It must contain only what you intended, in the
   module's own schema, with `tenant_id` on the table and `xmin` as
   `rowVersion: true` where expected. Compare with
   `src/Modules/MiniErp.Catalog/Migrations/20261114223650_InitialCatalog.cs`.
2. Check the model and migrations agree (expected: "No changes have been made to the
   model since the last migration."):
   ```bash
   dotnet ef migrations has-pending-model-changes --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext
   ```
3. Run `dotnet test`: every integration test applies all migrations to a fresh database.

Rules for migrations:

- Never edit or delete a migration that is on `main`. A correction is a new migration.
- A migration that is only on your branch and is wrong: take it out and add it again.
  `dotnet ef migrations remove` (same `--project`, `--startup-project`, `--context`)
  is the tool for that, but it has **not been run in this repository**, and it asks
  the database whether the migration was applied, which the design-time connection
  cannot answer. If it fails: delete the migration's two new files
  (`<timestamp>_<Name>.cs` and `.Designer.cs`) and restore the snapshot with
  `git restore src/Modules/MiniErp.<Name>/Migrations/<Name>DbContextModelSnapshot.cs`
  (for a module's very first migration, delete the snapshot too). Also not exercised;
  confirm with `has-pending-model-changes` after adding the migration again.
- Migration classes are public and live in `MiniErp.<Name>.Migrations`; the
  architecture test allows exactly that namespace.
- A connection error mentioning `mini_erp_design_time` during `dotnet ef migrations list`
  is harmless: the command tries the design-time connection string, fails and
  continues without the applied/pending status.

### First DbContext of a module (Inventory, Invoicing)

These two modules have only a module class today. To give one a database:

1. Create `<Name>DbContext.cs` in the module root, copying `CatalogDbContext.cs`:
   `internal sealed class <Name>DbContext(DbContextOptions<<Name>DbContext> options, ITenantContext tenantContext) : ModuleDbContext(options, tenantContext)`,
   with `SchemaName` (lower-case module name), `Schema`, and `ConfigureModel`. You cannot
   override `OnModelCreating`; it is sealed so the tenant filter is always applied.
2. In the same file add the design-time factory, copying
   `CatalogDbContextDesignTimeFactory`. `dotnet ef` needs it to build the context
   without a tenant.
3. In `<Name>Module.Add<Name>Module` call `services.AddModuleDbContext<<Name>DbContext>();`.
   That also registers the context for migrations at start-up.
4. Create the first migration. For Inventory (schema `inventory`):
   ```bash
   dotnet ef migrations add InitialInventory --project src/Modules/MiniErp.Inventory --startup-project src/MiniErp.Api --context InventoryDbContext --output-dir Migrations
   ```
5. No new package reference is needed: EF Core arrives through the reference to
   `MiniErp.Platform`.

Not exercised since PR #8 (Sales); follow the two existing contexts line by line. This
is the first step of phase 2. When it is done, add the module's row to the table above
and to mini-erp-build-and-run §5.

## 5. Contracts and validation

- Requests and responses are `internal sealed record`s.
- **Every request field is nullable** (`string?`, `decimal?`, `VatRate?`), so a missing
  value becomes a field error from validation instead of a deserialisation failure.
- The response has a static `From(entity)` method: mapping by hand, in one place.
- Lists return a page record whose first property is named after the resource: `ItemPage(IReadOnlyList<ItemResponse> Items, int Skip, int Take, int Total)`, `CustomerPage(... Customers, ...)`.
- Validation returns `Dictionary<string, string[]>` keyed by the **camelCase JSON field
  name** (`"unitPrice"`), with messages a user can act on; mention the rule id when one
  applies (`"... (BR-10)"`). Trim text before checking lengths.
- Enums travel as names (`"Standard"`); validate with `Enum.IsDefined` (true only for
  a value the enum declares).

## 6. Endpoint checklist

Copy `ItemEndpoints.cs`. For each endpoint:

- [ ] Mapped inside `Map<Entity>Endpoints(this RouteGroupBuilder module)`, on
      `module.MapGroup("/<plural>")`. Ids in routes are `{id:guid}`.
- [ ] `.RequireAuthorization(Permissions.X)`. Never a role, never nothing.
- [ ] `.WithName("VerbNoun")` (unique across the API), and `.WithSummary(...)` when the
      name does not say enough.
- [ ] The handler is a private static async method that takes the DbContext and a
      `CancellationToken`, and passes the token to every EF call.
- [ ] The return type is `Results<...>` listing every outcome, built with
      `TypedResults.*`, so OpenAPI shows each response.
- [ ] Reads use `AsNoTracking()`.
- [ ] Not found (including another tenant's id) → `TypedResults.NotFound()`. Do not add
      a tenant check by hand: the query filter already did it.
- [ ] Validation failure → `TypedResults.ValidationProblem(errors)` (400).
- [ ] Business conflict → `TypedResults.Problem(statusCode: 409, title, detail, extensions: { ["code"] = "<module>.<snake_case>" })`.
      Existing codes: `catalog.duplicate_sku`, `sales.duplicate_tax_id`. A code is part
      of the API: never rename one.
- [ ] A unique-index violation is caught as
      `DbUpdateException` whose inner exception is `PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }`.
      If the table has more than one unique index, also match `ConstraintName`
      (as `CustomerEndpoints.IsDuplicateTaxId` does).
- [ ] `GET` one and `POST` create set the ETag: `ConcurrencyHeaders.SetETag(response, entity.Version)`.
      Create returns `TypedResults.Created($"/api/<module>/<plural>/{id}", body)`.
- [ ] `PUT` follows this order exactly: load (404) → `ConcurrencyHeaders.TryReadIfMatch`
      (428 / 412) → compare `Version` (412) → validate (400) → change → `SaveChangesAsync`
      inside `try`, `catch (DbUpdateConcurrencyException)` → `ConcurrencyHeaders.PreconditionFailed()`
      → set the new ETag.
- [ ] Business operations are actions: `POST /{id}/<verb>`, returning `NoContent` or the
      changed resource.
- [ ] Master data is blocked, never deleted (`CLAUDE.md`; BR-12 for items and
      warehouses): no `MapDelete` for it. This is not a ban on every removal: lines of
      a draft order can be removed (specification §5).
- [ ] Lists of master data: `search` with `EF.Functions.ILike` (case-insensitive
      `LIKE`; AL: an `@*text*` filter), a stable `OrderBy`, `skip`/`take` clamped
      (default 50, maximum 100), and `Total`. **Lists that grow (ledger, documents) use
      cursor paging, `?after=...&limit=...` (specification §7)**; there is no reference
      implementation yet, so agree the shape with Stefan first.
- [ ] A new state-changing `POST`: BR-14 will require an `Idempotency-Key` header on
      all of them. That mechanism is not built and has no ADR
      (mini-erp-architecture-contract §6); do not invent a local version of it.
- [ ] A new conflict code follows `<module>.<snake_case>`. Already fixed by the
      documents for phase 2: `inventory.insufficient_stock`.

Then register the group in the module class:
`endpoints.MapModuleGroup(Name).MapItemEndpoints().Map<Entity>Endpoints();` (each
`Map...Endpoints` returns the module group so calls chain).

A number from a series: inject `INumberSeriesService` into the handler and call
`NextAsync(NumberSeriesCodes.X, cancellationToken)` **after** validation and before
`SaveChanges` (as `CustomerEndpoints.CreateAsync` does). It commits on its own, so a
failed save leaves a gap; that is allowed for these series (ADR-0009). Do not use it
for invoice numbers, which must be gapless and are not built yet.

## 7. Smaller recipes

**A new permission** (four places, or a test fails):

1. `src/MiniErp.Platform/Security/Permissions.cs`: the constant, and add it to `All`
   (the authorization policy is created from `All`).
2. `src/MiniErp.Platform/Security/RolePermissions.cs`: grant it to roles. `Admin` gets
   everything through `Permissions.All`.
3. `docs/specification.md` §2: the row in the permission table.
4. `tests/MiniErp.IntegrationTests/PermissionMatrixTests.cs`: one row per role in
   `Matrix`. `Every_permission_is_in_the_matrix` fails until you do.

Naming: lower-case, dot-separated, module first, like the existing five
(`catalog.read`, `sales.customers.write`, `platform.number-series.manage`).

**A new number series** (gaps allowed): add the code constant and its default format to
`NumberSeriesCodes` (`Defaults`). It then appears in `GET /api/platform/number-series`
and works with `NextAsync`. Update specification §4.5 and the expectations in
`NumberSeriesEndpointTests`.

**A new action on an existing entity:** a method on the entity, a `MapPost("/{id:guid}/<verb>")`
with a permission, the handler, a test for the action, a 403 test for a role without
the permission, and one more line in the entity's cross-tenant test.

**The first call from one module to another, or one transaction across two modules:**
stop. Neither the contract shape nor the shared-transaction mechanism is designed
(mini-erp-architecture-contract §5). Present options to Stefan; expect an ADR. The
other open questions for phase 2 are listed in mini-erp-architecture-contract §6.

## 8. Idioms in the existing code, for a Business Central developer

| In the code | What it is | Closest AL idea |
|---|---|---|
| `internal` | visible only inside its own project | `Access = Internal` on an object |
| `sealed` | the class cannot be inherited from | none needed; AL objects are not inherited |
| `internal sealed record CreateItemRequest(string? Sku, ...)` | a record: an immutable data class with value equality, declared in one line | none; the nearest is a read-only payload you pass around |
| `MapItemEndpoints(this RouteGroupBuilder catalog)` | an extension method: a static method called as if it belonged to the type of its first parameter | none; a procedure in a helper codeunit that takes the record as its first parameter |
| `DbSet<Item> Items` | the table as a queryable collection | a Record variable for the table |
| `class ItemEndpointTests(ApiFactory api)` | a primary constructor: parameters declared on the class and usable in its body | no direct equivalent; like `SetParameters` done at creation |
| `string?` versus `string` | nullable reference types: the compiler tracks which values may be null | none; AL values are never null |
| `request.Sku!` | "I know this is not null here" (after validation) | none |
| `{ get; private set; }` | only the class itself can change the property | a field that is editable only through a procedure |
| `async Task<...>` / `await` | the thread is released while the database works; the method continues afterwards | none; AL is synchronous |
| `CancellationToken` | lets the server stop work when the client disconnects | none |
| `db.Items.Where(...).OrderBy(...).ToListAsync()` | LINQ: a query built in C# and translated to SQL | `SetRange` / `SetCurrentKey` / `FindSet` |
| `db.Items.Add(item)` then `SaveChangesAsync()` | change tracking: EF records changes and writes them in one transaction | `Insert` / `Modify`, but applied at `SaveChanges` rather than line by line |
| `AsNoTracking()` | read rows without tracking them for changes | a read-only `FindSet(false)` |
| `Results<Ok<T>, NotFound, ...>` with `TypedResults` | the handler's possible HTTP answers, checked by the compiler | none |
| method parameters `CatalogDbContext db, HttpResponse response` | dependency injection: the framework creates them per request and passes them in | none; nearest is an interface whose implementation the platform picks for you |
| `[GeneratedRegex("...")] private static partial Regex X();` | a regular expression compiled at build time | none |
| `catch (...) when (condition)` | an exception filter: catch only when the condition holds | `if not TryFunction() then` with a check on `GetLastErrorCode` |
| `is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }` | a property pattern: "is of this type and its property has this value" | a type check plus a field comparison |
| `await using var scope = ...` | dispose the object (asynchronously) when the block ends | none; cleanup is automatic in AL |
| `item with { Name = "x" }` (in tests) | a copy of a record with one value changed | none |

When such a feature appears in new code for the first time, explain it briefly to
Stefan in the conversation (working agreement 7).

## 9. Traps

| Trap | What happens | Avoid by |
|---|---|---|
| Entity without `ITenantOwned` | rows are visible to every tenant; no test fails until a cross-tenant test exists | the entity checklist; write the cross-tenant test first |
| A unique index without `TenantId` | tenant B cannot use a SKU tenant A used | start every unique index with `TenantId`; test "the same value in another tenant is allowed" |
| A public type in a module | `Module_exposes_only_its_registration_class_and_contracts` fails | `internal` by default |
| A `ProjectReference` from one module to another | `Module_does_not_depend_on_another_module` fails | contracts only; see §7, last recipe |
| Retrying after `DbUpdateConcurrencyException` with the same DbContext (phase 2, ADR-0008; not built) | the context still tracks the stale rows, so the retry fails again or saves wrong data | a retry starts from a fresh read in a fresh scope; design the helper with Stefan before coding |
| Error dictionary keyed `"UnitPrice"` | clients and tests expect `"unitPrice"` | camelCase keys |
| Validating before the `If-Match` check in `PUT` | a stale client gets 400 instead of 412; differs from the reference | the order in §6 |
| Changing an entity without a migration | `has-pending-model-changes` reports changes; tests fail on a missing column | always run the check |
| An unused `using`, parameter or variable | the build fails: warnings are errors | fix it, do not suppress |
| Raw SQL | bypasses the tenant filter and the interceptor | avoid; it needs Stefan's agreement before it is written (mini-erp-change-control §7) and always a tenant parameter |

## When NOT to use this skill

| You need | Use |
|---|---|
| Which tests to write and how | mini-erp-testing-and-qa |
| Why a rule exists, or whether a design is allowed | mini-erp-architecture-contract |
| Adding a package the feature needs | mini-erp-change-control §5 |
| A setting rather than code | mini-erp-config-and-flags |
| Updating the specification, README or writing an ADR | mini-erp-docs-and-writing |
| Stock reservation or the inventory ledger (phase 2's hard part) | ADR-0004 and ADR-0008 first, then mini-erp-architecture-contract §6 (what is not built, open questions) and mini-erp-testing-and-qa §5 (the proof standard). A dedicated campaign skill is planned (`CLAUDE.md`, "Skill library") |

## Provenance and maintenance

Written 2026-11-15 from `src/Modules/MiniErp.Catalog`, `src/Modules/MiniErp.Sales`,
`src/MiniErp.Platform` and PRs #6-#8. The `dotnet ef` commands for `list` and
`has-pending-model-changes` were run that day; `migrations add` was last run for PR #8;
`migrations remove` has not been run here. Re-verify what may drift:

```bash
ls src/Modules/MiniErp.Catalog/Items src/Modules/MiniErp.Sales/Customers   # the file set of a feature
git grep -n "RequireAuthorization" -- src/Modules                          # every endpoint has a permission
git grep -n '\["code"\]' -- src                                            # existing error codes
git grep -n "AddModuleDbContext<" -- src                                   # which modules have a DbContext
cat src/MiniErp.Platform/Security/Permissions.cs                           # current permissions
dotnet ef migrations has-pending-model-changes --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext
```
