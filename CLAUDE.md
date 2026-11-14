# CLAUDE.md

Guidance for Claude Code sessions in this repository.

## What this is

**mini-erp** is a multi-tenant ERP backend on .NET: catalog → inventory → sales orders → shipments → invoices → e-invoice submission. It is modelled on how Microsoft Dynamics 365 Business Central posts documents. It is a **portfolio and learning project** for Stefan, not a product. Scope decisions optimise for showing medior-level backend engineering, not for features.

- Specification (scope, domain model, business rules `BR-xx`, phases): [docs/specification.md](docs/specification.md)
- Architecture decisions: [docs/adr/](docs/adr/README.md)

**Status (2026-11-14):** phase 1 complete (specification §8): foundation, auth, tenancy, number series, Catalog items, Sales customers. Next: phase 2 (inventory and sales orders). ADRs 0001–0010 are `Accepted`. Public repository: https://github.com/stefanbabic5/mini-erp. Work lands through pull requests, squash-merged into `main`.

## Working agreements

### 1. Free dependencies only ([ADR-0007](docs/adr/0007-free-dependencies-only.md))
- Only OSI-licensed libraries (MIT, Apache 2.0, BSD, PostgreSQL) or free tiers that need no payment and no card.
- **Never add:** MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+, NServiceBus, or any paid cloud service.
- Before adding a package or bumping a major version, check its current licence and state it when proposing the change.
- Cloud-specific services only behind an interface with a local implementation that runs in Docker.

### 2. Decisions are written down
- Any significant decision (a new dependency, a pattern, a data-shape choice, a trade-off) gets an ADR using the template in [docs/adr/README.md](docs/adr/README.md), with the rejected alternatives and why.
- An `Accepted` ADR is never rewritten. A changed decision gets a new ADR that supersedes it.
- The specification is a living document: when behaviour changes, update it in the same change.

### 3. Business rules are traceable
- Every rule in specification §6 has an id (`BR-01`…). Tests that prove a rule carry its id in their name, e.g. `BR02_parallel_releases_never_make_available_negative`.
- A new rule gets a new id; ids are never reused.

### 4. Evidence over claims
- Never say something works without running it. Report test results as they are.
- Integration tests run against real PostgreSQL (Testcontainers). Do not mock the database.
- Concurrency rules (BR-02, BR-09) are proven with parallel tests, not by reasoning alone.
- The README may only claim what a test, a measurement or the code proves.

### 5. Git
- Never commit to `main`. Work on branches: `feature/`, `fix/`, `docs/`, `test/`, `chore/`.
- Never commit, push or create a remote without Stefan's explicit go-ahead in the conversation.

### 6. Language
- Everything in the repository is in **English**: code, comments, docs, commit messages.
- Conversation with Stefan is in **Serbian**.

### 7. Teaching mode
Stefan is a Business Central / AL developer moving to .NET. He knows OOP well and has done some C# on Microsoft Learn.
- The first time a C# or .NET feature appears in code (records, pattern matching, nullable reference types, `async`/`await`, LINQ, dependency injection, EF Core change tracking, middleware, and so on), explain briefly what it is and why it is used here.
- Compare with BC/AL concepts where that helps (see the BC column in specification §3).
- Prefer explicit, readable code over clever or magic code. Stefan must be able to explain every line in an interview.
- When there is a real design choice, present the options with a recommendation instead of choosing silently.
- Personal instructions (such as where Stefan's study notes live) are in `CLAUDE.local.md`, which is not part of the repository.

## Architecture in one screen

- **Modular monolith** ([ADR-0001](docs/adr/0001-modular-monolith.md)): modules Catalog, Inventory, Sales, Invoicing, plus Platform. Each module owns its own PostgreSQL schema and `DbContext`, and exposes a small public contract. Modules never reference each other's internals; architecture tests enforce this.
- **PostgreSQL + EF Core** ([ADR-0002](docs/adr/0002-postgresql-and-ef-core.md)), `xmin` as the concurrency token.
- **Multi-tenancy** ([ADR-0003](docs/adr/0003-multi-tenancy.md)): tenant only from the token's `tenant_id` claim; global query filters; another tenant's id answers `404`. Every module DbContext derives from `ModuleDbContext` (own schema, own migrations history, a tenant query filter on every `ITenantOwned` entity) and is registered with `AddModuleDbContext<T>()`, which adds `TenantSaveChangesInterceptor` (stamps the tenant on inserts, throws `CrossTenantWriteException` on writes to another tenant's rows). Raw SQL bypasses the filter and must filter by tenant itself.
- **Number series** (`src/MiniErp.Platform/Numbering/`): `INumberSeriesService.NextAsync(code)` is one atomic upsert in its own short transaction (gaps allowed, never duplicates).

## Endpoint conventions (see `src/Modules/MiniErp.Catalog/Items/` as the reference)

- One folder per feature inside a module: entity, request/response records (`*Contracts.cs`), validation, endpoints. All `internal`.
- Each endpoint requires a permission (`.RequireAuthorization(Permissions.X)`) and returns typed results (`Results<Ok<T>, NotFound, ValidationProblem, ...>`), so OpenAPI lists every response.
- Validation returns `TypedResults.ValidationProblem(errors)` keyed by field name; business conflicts return a Problem with a stable `code` extension (e.g. `catalog.duplicate_sku`).
- Another tenant's id is simply not found by the filtered query, so it answers 404 (BR-13).
- Updates use optimistic concurrency: responses carry `ETag` (the row's `xmin`); `PUT` requires `If-Match` (428 without it, 412 on a stale version) via `Platform/Http/ConcurrencyHeaders`.
- Business operations are actions (`POST /items/{id}/block`), not field edits; nothing is deleted (BR-12).
- Enums travel as names (`"Standard"`), money as `decimal` with 2 decimals (BR-10).
- Tests: one class per feature under `tests/MiniErp.IntegrationTests/<Module>/`, fresh tenant ids per test, a cross-tenant test that tries every endpoint, and the business rule id in the test name.
- **Inventory ledger** ([ADR-0004](docs/adr/0004-append-only-inventory-ledger.md)): append-only entries plus a balance projection updated in the same transaction.
- **Transactional outbox** ([ADR-0005](docs/adr/0005-transactional-outbox.md)) for e-invoice submission; at-least-once delivery, idempotent handlers.
- **Keycloak** ([ADR-0006](docs/adr/0006-keycloak-for-identity.md)) as OIDC provider; tests sign their own tokens (`tests/MiniErp.IntegrationTests/TestTokens.cs`).
- **Security** (`src/MiniErp.Platform/Security/`): endpoints require a *permission* (`Permissions`, used as the policy name), never a role; `RolePermissions` is the only role → permission matrix (specification §2). A fallback policy makes every endpoint require an authenticated user with a valid `tenant_id` unless it calls `AllowAnonymous()`. Code gets the tenant from `ITenantContext`, never from the request.
- **Stock reservation** uses optimistic concurrency with a retry of the whole unit of work ([ADR-0008](docs/adr/0008-stock-reservation-concurrency.md)). **Invoice numbers** are gapless through a row lock on the number series ([ADR-0009](docs/adr/0009-document-numbering.md)).

## Prerequisites

- .NET SDK 10
- Docker with Linux containers (Docker Desktop with the WSL 2 backend on Windows; see [ADR-0010](docs/adr/0010-docker-desktop-for-local-containers.md))

## Repository layout

```
MiniErp.slnx                 solution (XML .slnx format)
global.json                  pins the .NET SDK; opts dotnet test into Microsoft.Testing.Platform
Directory.Build.props        shared build settings: net10.0, nullable, warnings as errors
Directory.Packages.props     every NuGet version, with its licence (central package management)
compose.yaml                 local infrastructure: PostgreSQL 18, Keycloak and the OpenTelemetry dashboard
.env.example                 optional overrides for compose.yaml (copy to the git-ignored .env)
deploy/keycloak/             realm imported by Keycloak on start: tenants Alfa and Beta, one user per role
src/MiniErp.Api/             API host: wires modules together, no business logic
  Hosting/                   observability (OpenTelemetry), health endpoints, OpenAPI bearer scheme
src/MiniErp.Platform/        shared building blocks: module endpoint convention, Database/ (NpgsqlDataSource, ModuleDbContext, tenant interceptor, migration runner, health check), Security/ (JWT, permissions, tenant context, /api/platform/me), Numbering/ (number series), PlatformDbContext + Migrations/ (schema platform); later outbox
dotnet-tools.json            local tools: dotnet-ef (run `dotnet tool restore` once)
src/Modules/MiniErp.<Name>/  one project per module: Catalog (items), Sales (customers), Inventory, Invoicing; each with its DbContext and Migrations/
tests/MiniErp.ArchitectureTests/  module boundary rules (ADR-0001), checked on the compiled assemblies
tests/MiniErp.IntegrationTests/   the real API in memory against PostgreSQL in Docker (Testcontainers)
.github/workflows/ci.yml     build + test on every push to main and every pull request
```

Each module exposes only `<Name>Module` (with `Add<Name>Module` and `Map<Name>Module`) and types in `MiniErp.<Name>.Contracts`; everything else is `internal`. A module never references another module's project.

## Commands

```bash
docker compose up -d                  # start PostgreSQL, Keycloak and the dashboard
dotnet build                          # build everything
dotnet test                           # run all tests; integration tests need Docker running
dotnet run --project src/MiniErp.Api  # start the API on http://localhost:5100 (applies pending migrations in Development)
docker compose down                   # stop the containers (add -v to delete the database volume)
```

Migrations (one set per module DbContext; the API project is the startup project):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext --output-dir Migrations
```

Platform uses `--project src/MiniErp.Platform --context PlatformDbContext`. A module context needs an `IDesignTimeDbContextFactory` (see `CatalogDbContextDesignTimeFactory`) so `dotnet ef` can build it without a tenant.

| URL (development) | What |
|---|---|
| http://localhost:5100/scalar | API reference (Scalar UI over the OpenAPI document) |
| http://localhost:5100/openapi/v1.json | OpenAPI document |
| http://localhost:5100/health/live, /health/ready | liveness (process up) and readiness (PostgreSQL reachable) |
| http://localhost:18888 | dashboard: logs, traces and metrics from the API |
| http://localhost:8080 | Keycloak admin console (admin / admin), realm `mini-erp` |
| http://localhost:5100/api/platform/me | who the API thinks you are: user, tenant, roles, permissions |

Local users (password `dev-password`): `alfa.admin`, `alfa.sales`, `alfa.warehouse`, `alfa.finance` and the same four for `beta`. Get a token for manual calls (development client, password grant):

```bash
curl -s http://localhost:8080/realms/mini-erp/protocol/openid-connect/token -d grant_type=password -d client_id=mini-erp-dev -d username=alfa.sales -d password=dev-password
```

Errors are RFC 9457 Problem Details (`application/problem+json`). Telemetry is exported only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (the `http` launch profile sets it to the dashboard).

## Skill library

Not created yet. With phase 1 complete, a `.claude/skills/` library is generated next, in its own session, grounded in this code, its tests and its git history. Until it exists, this file is the only project guide.
