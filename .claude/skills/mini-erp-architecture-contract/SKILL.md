---
name: mini-erp-architecture-contract
description: >
  The architecture contract of mini-erp: the load-bearing design decisions with the
  reason for each, the invariants that must hold with the test that proves each, and
  the known weak points stated plainly. Load this BEFORE changing anything in
  src/MiniErp.Platform (ModuleDbContext, TenantSaveChangesInterceptor,
  DatabaseRegistration, SecurityRegistration, RolePermissions, ConcurrencyHeaders,
  NumberSeriesService), before making one module use another, before adding a project
  reference, a public type, a Contracts namespace, a cross-module call or
  transaction, a background job, IgnoreQueryFilters, raw SQL or AllowAnonymous, when
  an architecture test fails, when asked "why is it built this way" or "is this design
  allowed here", and when planning phase 2 work (warehouses, inventory ledger, sales
  orders, stock reservation) to see which decisions are taken, which are not built
  and which design questions are still open. Not a how-to: the
  step-by-step recipe for a feature is mini-erp-add-feature.
---

# mini-erp architecture contract

What must stay true in **mini-erp** (multi-tenant ERP backend, .NET 10, PostgreSQL 18),
and why. The decisions themselves live in `docs/adr/`; this skill does not repeat them.
It maps each decision to the code that implements it, the invariant it creates, the
test that proves the invariant, and the ways a change can break it.

State on 2026-11-15: phase 1 is complete. Catalog (items) and Sales (customers) have
code; Inventory and Invoicing are empty module shells. Anything about the ledger, stock
reservation, the outbox, idempotency keys or gapless invoice numbers is **decided but
not built** (§6).

Terms, defined once:

- **Module**: one project under `src/Modules/` (Catalog, Inventory, Sales, Invoicing).
  **Platform** (`src/MiniErp.Platform`) is the shared project every module references.
- **Tenant**: one company using the system. Business Central equivalent: a Company
  (not BC's own "tenant", which is a customer's whole environment).
- **`internal`**: visible only inside its own project (assembly). AL equivalent:
  `Access = Internal` on an object. `InternalsVisibleTo` opens a project's internals to
  one named project, like `internalsVisibleTo` in `app.json`.
- **DbContext**: EF Core's unit of work and the gateway to the database; here, one per
  module.
- **Query filter**: a condition EF Core adds to every query on an entity type. Think
  of a security filter that code cannot forget to set.
- **Interceptor**: code EF Core runs before `SaveChanges` writes to the database, like
  one `OnBeforeInsert`/`OnBeforeModify` subscriber for every table.
- **`xmin`**: a system column PostgreSQL keeps on every row; it changes on every update.
- **Fallback policy**: the authorization rule applied to an endpoint that declares none.

## 1. The decisions and where they live

| Decision | ADR | Implemented in | Business Central comparison |
|---|---|---|---|
| Modular monolith: one deployable, modules with their own schema and DbContext | [0001](../../../docs/adr/0001-modular-monolith.md) | `src/MiniErp.Api/Program.cs` (wires modules), `src/Modules/MiniErp.<Name>/<Name>Module.cs` | Like apps/extensions that may only use each other's public objects |
| PostgreSQL + EF Core, `xmin` as the row version | [0002](../../../docs/adr/0002-postgresql-and-ef-core.md) | `Platform/Database/DatabaseRegistration.cs`, each `<Name>DbContext` (`.IsRowVersion()` on `Version`) | `xmin` plays the role of the record's `timestamp`/`SystemRowVersion` |
| Shared tables with a tenant column; tenant only from the token | [0003](../../../docs/adr/0003-multi-tenancy.md) | `Platform/Database/ModuleDbContext.cs`, `TenantSaveChangesInterceptor.cs`, `Platform/Security/TenantContext.cs` | BC separates companies by table name; here it is a column plus a filter |
| Keycloak issues tokens; the API only validates them | [0006](../../../docs/adr/0006-keycloak-for-identity.md) | `Platform/Security/SecurityRegistration.cs`, `deploy/keycloak/realm-mini-erp.json` | Like Entra ID in front of BC |
| Permissions, not roles, on endpoints | specification §2 | `Platform/Security/Permissions.cs`, `RolePermissions.cs`, `AuthorizationRequirements.cs` | Permission sets assigned to users |
| Number series: gaps allowed for CUST/SO/SHIP | [0009](../../../docs/adr/0009-document-numbering.md) | `Platform/Numbering/NumberSeriesService.cs` | No. Series |
| Optimistic concurrency over HTTP (ETag / If-Match) | specification §7 | `Platform/Http/ConcurrencyHeaders.cs` | BC's "another user has modified the record" error |
| Free dependencies only | [0007](../../../docs/adr/0007-free-dependencies-only.md) | `Directory.Packages.props` | see mini-erp-change-control |

## 2. Invariants and their proofs

Each row is something a change must not break. "Proof" is the test to run and to keep
green; if you change the code in the middle column, run that test and do a planted-bug
check (mini-erp-change-control §6).

| # | Invariant | Enforced by | Proof (test) |
|---|---|---|---|
| I1 | A module never references another module's project. | Project references; nothing else stops it at compile time | `ModuleBoundaryTests.Module_does_not_depend_on_another_module` |
| I2 | Platform references no module. | same | `ModuleBoundaryTests.Platform_does_not_depend_on_any_module` |
| I3 | A module's only public types are `<Name>Module`, types in `MiniErp.<Name>.Contracts`, and EF's generated `MiniErp.<Name>.Migrations` classes. | `internal` on everything else | `ModuleBoundaryTests.Module_exposes_only_its_registration_class_and_contracts` |
| I4 | The tenant comes only from the token's `tenant_id` claim. | `HttpTenantContext` reads `User.FindTenantId()`; no code reads a tenant from route, header or body | `AuthenticationTests` (missing, non-GUID and empty tenant answer 403) |
| I5 | Every query on an `ITenantOwned` entity returns only the current tenant's rows. | `ModuleDbContext.OnModelCreating` (sealed) adds the filter to every `ITenantOwned` type | `TenantIsolationTests.BR13_queries_return_only_the_current_tenants_rows` |
| I6 | An insert gets the current tenant; an insert, update or delete of another tenant's row throws `CrossTenantWriteException`, even after `IgnoreQueryFilters()`. | `TenantSaveChangesInterceptor`, attached by `AddModuleDbContext<T>()` | `TenantIsolationTests.BR13_insert...`, `BR13_inserting_a_row_for_another_tenant_is_refused`, `BR13_changing...`, `BR13_deleting...` |
| I7 | Another tenant's id answers 404, never 403 (BR-13). | Follows from I5: the filtered query finds nothing | `BR13_another_tenants_item_is_not_found_on_any_endpoint`, `BR13_another_tenants_customer_is_not_found_on_any_endpoint` |
| I8 | Every endpoint needs an authenticated user with a valid tenant unless it calls `AllowAnonymous()`. | `FallbackPolicy` in `SecurityRegistration` | `AuthenticationTests.Request_without_token_answers_401_as_problem_details`, `Token_without_tenant_answers_403` |
| I9 | Who may do what is decided only in `RolePermissions`, and matches specification §2. | Endpoints call `.RequireAuthorization(Permissions.X)`, never check a role | `PermissionMatrixTests` (the expected matrix is written out in the test on purpose) |
| I10 | A number from `INumberSeriesService.NextAsync` is never handed out twice, per tenant and series. | One `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` statement | `NumberSeriesTests.BR09_parallel_requests_never_get_the_same_number` (50 parallel calls) |
| I11 | An update based on a stale version does not overwrite a newer one. | `If-Match` compared with `Version`, then EF adds `WHERE xmin = ...` to the `UPDATE` | `Update_with_a_stale_etag_answers_412_and_keeps_the_other_change` (items), `Update_with_a_stale_etag_answers_412_and_without_one_428` (customers) |
| I12 | Items and customers are never deleted. | No `DELETE` endpoint is mapped | `BR12_items_cannot_be_deleted`, `Customers_cannot_be_deleted` (405) |
| I13 | The API starts and reports "not ready" while PostgreSQL is down. | `/health/ready` runs `DatabaseHealthCheck`; migrations run on start only when `Database:MigrateOnStartup` is true | `HostingTests.Readiness_probe_answers_503_when_postgres_is_unreachable_while_liveness_stays_healthy` |
| I14 | Errors leave the API as RFC 9457 Problem Details. | `AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages` in `Program.cs` | `HostingTests.Unknown_route_answers_404_as_problem_details` |

Run them all: `dotnet test`. Run the boundary tests alone:
`dotnet test --project tests/MiniErp.ArchitectureTests`.

## 3. How tenancy works, end to end

1. The token carries `tenant_id`. `MapInboundClaims = false` keeps claim names as the
   token has them.
2. `HttpTenantContext.TenantId` reads the claim from the current `HttpContext`. It
   **throws** when there is no tenant: that is a bug, because the fallback policy
   already refused such a request.
3. `ModuleDbContext` holds `ITenantContext` and exposes a private `CurrentTenantId`
   property. The query filter `entity.TenantId == CurrentTenantId` refers to that
   property, so EF Core sends the tenant as a query parameter. The model is built once
   and cached, yet every request filters by its own tenant.
4. `OnModelCreating` is `sealed`: a module configures its entities in
   `ConfigureModel`, and cannot forget the filter.
5. On save, the interceptor stamps `TenantId` on added rows whose tenant is empty, and
   refuses every other mismatch.

Consequences you must respect:

- An entity is tenant-scoped **only if it implements `ITenantOwned`**. A new entity
  without it has no filter and no guard. Every unique index on a tenant-owned entity
  starts with `TenantId` (`(TenantId, Sku)`, `(TenantId, TaxId)`).
- **Raw SQL bypasses the filter and the interceptor.** `NumberSeriesService` is the one
  raw statement that touches tenant data today (the health check's `SELECT 1` touches
  none); it passes `tenant_id` as a parameter. Any new raw SQL must do the same and say
  so in a comment.
- `IgnoreQueryFilters()` appears only in tests today
  (`git grep -n "IgnoreQueryFilters()" -- src` finds nothing).
- New raw SQL, `IgnoreQueryFilters()` or `AllowAnonymous()` in `src/` needs Stefan's
  agreement before it is written: mini-erp-change-control §7.
- Code outside an HTTP request (a background job) has no `HttpContext`, so
  `ITenantContext.TenantId` throws. ADR-0003 says such code must set the tenant from the
  message it processes; that mechanism is not built yet.

## 4. How a request is authorized

`Program.cs` order: `UseExceptionHandler` → `UseStatusCodePages` → `UseAuthentication`
→ `UseAuthorization` → endpoints.

- No token, expired token, wrong audience, untrusted key: **401**.
- Valid token without a valid tenant, or without the permission: **403**.
- Valid token, permission granted, id belongs to another tenant: **404**.

Anonymous endpoints, complete list: `/health/live`, `/health/ready`, and in the
Development environment only `/openapi/v1.json` and `/scalar`.

Adding a permission touches four places: mini-erp-add-feature §7.

## 5. How modules are wired

- `Program.cs` calls `AddMiniErpDatabase`, `AddMiniErpSecurity`, then
  `Add<Name>Module()` for each module, then `MapPlatformEndpoints()` and
  `Map<Name>Module()`. The host has no business logic.
- `AddModuleDbContext<T>()` registers the context on the one shared `NpgsqlDataSource`
  (one connection pool), with snake_case names and the tenant interceptor, and records
  the type so `MigrateModuleDatabasesAsync` applies its migrations on start-up when
  `Database:MigrateOnStartup` is true.
- Each context keeps its own `__ef_migrations_history` table in its own schema, so
  modules migrate independently.
- Platform has `InternalsVisibleTo("MiniErp.IntegrationTests")`; modules do not. Module
  behaviour is tested through HTTP.

### Cross-module calls and transactions: not designed yet

**No module calls another module yet**, and no code opens a transaction across two
DbContexts (`git grep -n "BeginTransaction\|UseTransaction" -- src` finds nothing).
Phase 2 needs both at once:

| Need | What the documents fix | What is open |
|---|---|---|
| A sales order line reads an item's price and blocked flag (Sales → Catalog) | ADR-0001: only through a public contract in `MiniErp.<Name>.Contracts`, called in-process | where the interface lives and how the host connects two modules that may not reference each other |
| Releasing an order changes the order (Sales) and reserves stock (Inventory) atomically | ADR-0001: "Operations that must be atomic across modules ... share one database transaction" | the mechanism: each module's DbContext takes its own connection from the shared `NpgsqlDataSource`, so two contexts do not share a transaction by themselves |

Both are real design choices. Stop, present options with a recommendation to Stefan,
and expect an ADR before code.

## 6. Decided but not built

Do not describe any of these as existing. Each has an ADR; none has code or a test.

| Topic | ADR | What is missing |
|---|---|---|
| Append-only inventory ledger with a balance projection (BR-01). BC: Item Ledger Entry | [0004](../../../docs/adr/0004-append-only-inventory-ledger.md) | Entities, the database role without UPDATE/DELETE on the ledger (a second role and connection: mini-erp-config-and-flags §8), the reconciliation test |
| Stock reservation with optimistic concurrency and whole-unit-of-work retry (BR-02) | [0008](../../../docs/adr/0008-stock-reservation-concurrency.md) | The retry helper ("a single helper used by every command that writes balances", so it belongs in Platform), fixed order by item id, the parallel release test. A retry must start from a fresh read: a DbContext whose save failed still tracks the stale rows |
| Transactional outbox | [0005](../../../docs/adr/0005-transactional-outbox.md) | Table, dispatcher, tenant context for background work |
| Gapless invoice numbers (`INV`, `CM`) | [0009](../../../docs/adr/0009-document-numbering.md) | Row lock inside the posting transaction; yearly reset |
| Idempotency keys (BR-14) | none yet; specification §6 and §4.5 (`IdempotencyRecord`) | Everything, including the ADR. It is Platform-wide and will change every state-changing `POST` |
| Row-level security as a second tenant guard | [0003](../../../docs/adr/0003-multi-tenancy.md) | Phase 4 stretch |

### Open design questions for phase 2

No document answers these. Each is Stefan's decision; several need an ADR.

1. The cross-module contract and the cross-module transaction (§5).
2. Another tenant's id in a request **body** (`customerId`, `itemId`, `warehouseId` on
   an order). BR-13 fixes 404 for an id in the route; the answer for a body field
   (404, or a 400/409 validation error) is not decided.
3. The ETag of a document with lines: the ETag is the header row's `xmin`, and changing
   a line does not change the header row.
4. The type and precision of quantities. BR-10 covers money only.
5. Where `PostedBy` comes from: nothing exposes the current user id as a service yet
   (only `CurrentUserEndpoint` reads the `sub` claim).
6. How a test reads another module's tables for the ledger reconciliation (module
   internals are not visible to the test project).
7. Cursor paging for lists that grow (specification §7) has no reference
   implementation.

Names the documents already fix for phase 2: error code `inventory.insufficient_stock`
(ADR-0008, specification §7); action routes `/release`, `/reopen`, `/cancel`,
`/close-remainder`, `/shipments` under `/api/sales/orders/{id}` (specification §7); the
series `SO` and `SHIP` already exist in `NumberSeriesCodes`.

## 7. Known weak points (2026-11-15)

Stated plainly, so nobody discovers them by surprise or claims the opposite.

1. **Boundaries are checked at the assembly level only.** The tests see project
   references and public types. They would not notice a module querying another
   module's schema with raw SQL.
2. **Block and unblock ignore the row version.** `POST .../block` and `/unblock` return
   204 with no ETag and need no `If-Match`. Only `PUT` is protected against lost
   updates.
3. **`CreateAsync` for items treats any unique violation as a duplicate SKU.** Correct
   while `items` has one unique index; a second one would produce a wrong message.
   Customers check the constraint name (`ix_customers_tenant_id_tax_id`), which in turn
   depends on the snake_case naming convention.
4. **Three requirements of specification §9 are not met**: unit tests on the domain,
   tenant and user id in the logs, a dependency scan in CI. The list of places where
   documents say more than the code does is owned by mini-erp-docs-and-writing §7.
5. **Migrations run at start-up in Development only.** `Program.cs` says other
   environments apply them "as a separate deployment step"; that step does not exist,
   because there is no deployment.
6. **Number-series endpoints have no ETag.** Two admins changing a format at once: the
   last write wins.
7. **`main` is not protected on GitHub.** See mini-erp-change-control.

Fixing any of these is a normal change through change control, not a silent cleanup.

## When NOT to use this skill

| You need | Use |
|---|---|
| Steps to add a feature, endpoint, entity, migration or permission | mini-erp-add-feature |
| How to write or run a test | mini-erp-testing-and-qa |
| Settings, ports, connection strings | mini-erp-config-and-flags |
| Starting the stack, getting a token | mini-erp-build-and-run |
| Writing an ADR for a new decision | mini-erp-docs-and-writing |

## Provenance and maintenance

Written 2026-11-15 from the ADRs, `src/MiniErp.Platform`, both implemented modules and
the test suite (all green that day; totals: mini-erp-testing-and-qa). Re-verify what may drift:

```bash
dotnet test --project tests/MiniErp.ArchitectureTests            # I1-I3
dotnet test --project tests/MiniErp.IntegrationTests --filter-method "*BR13*"   # I5-I7
git grep -n "IgnoreQueryFilters()" -- src                       # expect no result
git grep -n "\.AllowAnonymous()" -- src                         # expect HealthEndpoints.cs and Program.cs only
git grep -l "ITenantOwned" -- src                                # every tenant-scoped entity (plus Platform/Database)
ls src/Modules/MiniErp.Inventory src/Modules/MiniErp.Invoicing   # still shells? then §6 still holds
ls docs/adr/                                                     # a new ADR may change §1 or §6
```
