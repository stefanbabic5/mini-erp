---
name: mini-erp-config-and-flags
description: >
  Catalogue of every configuration axis in mini-erp. Adding or bumping a package,
  container image or tool is gated: load mini-erp-change-control first, then this.
  Load this when reading, adding or changing a setting in appsettings.json,
  appsettings.Development.json, launchSettings.json, compose.yaml, .env /
  .env.example, global.json, Directory.Build.props, Directory.Packages.props or
  dotnet-tools.json; when editing the Keycloak realm
  (deploy/keycloak/realm-mini-erp.json: a demo user, role, client, claim or tenant);
  when a port, connection string, JWT authority or audience must change; when asking
  "where does this value come from", "what is the default", "is this
  development-only", "why does the API not pick up my .env"; when the API refuses to
  start because configuration is missing; and when a constant in code acts as a
  switch (number series defaults, paging limits, permission names). Includes the
  checklist for adding a setting. How to start things is in mini-erp-build-and-run.
---

# mini-erp configuration and flags

Every knob in **mini-erp**, where it is set, its default, and who reads it. Values were
read from the files on 2026-11-15; each section ends in a command that prints the
current truth. When this skill and a file disagree, the file wins.

There are **no feature flags** in this project. "Flags" here means settings and the few
code constants that act as switches.

Terms, defined once:

- **Configuration key**: a name such as `Database:MigrateOnStartup`. In JSON the colon
  is nesting; as an environment variable it becomes a double underscore
  (`Database__MigrateOnStartup`).
- **Environment**: the value of `ASPNETCORE_ENVIRONMENT`. `Development` makes ASP.NET
  Core also read `appsettings.Development.json`.
- **Launch profile**: a named set of start options in `launchSettings.json`, used by
  `dotnet run`. It plays the role of `launch.json` in an AL project.

For a Business Central reader: `global.json` and `Directory.Packages.props` together do
what `platform`, `runtime` and `dependencies` do in `app.json`. Settings here live in
files and environment variables; what BC keeps in Setup tables is **data** here too
(for example a tenant's number format, §5).

## 1. Runtime settings of the API

Sources, later ones override earlier ones: `src/MiniErp.Api/appsettings.json` →
`appsettings.Development.json` (Development only) → environment variables.

| Key | `appsettings.json` | `appsettings.Development.json` | Read by | If missing |
|---|---|---|---|---|
| `ConnectionStrings:MiniErp` | not set | `Host=localhost;Port=5432;Database=mini_erp;Username=mini_erp;Password=mini_erp_dev` | `DatabaseRegistration.AddMiniErpDatabase` | start-up throws "Connection string 'MiniErp' is missing" |
| `Database:MigrateOnStartup` | not set (false) | `true` | `Program.cs` | migrations are not applied; the API still starts |
| `Authentication:Jwt:Authority` | not set | `http://localhost:8080/realms/mini-erp` | `SecurityRegistration.AddMiniErpSecurity` | start-up throws "...Authority and ...Audience must be configured" |
| `Authentication:Jwt:Audience` | `mini-erp-api` | inherited | same | same |
| `Authentication:Jwt:RequireHttpsMetadata` | `true` | `false` | same | defaults to `true` in code |
| `Logging:LogLevel:Default` / `Microsoft.AspNetCore` | `Information` / `Warning` | same | ASP.NET Core logging | framework defaults |
| `AllowedHosts` | `*` | inherited | ASP.NET Core host filtering | |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | not set | not set | `ObservabilityRegistration.AddObservability` | no telemetry is exported (by design) |

The development connection string and the absence of secrets are deliberate: the
values are local defaults that match `compose.yaml`. Nothing here is a real credential.

**Development-only behaviour** (anything guarded by the environment or by a
Development-only value):

- `/openapi/v1.json` and `/scalar` are mapped only when `app.Environment.IsDevelopment()`.
- Migrations on start-up (`Database:MigrateOnStartup`).
- HTTP (not HTTPS) metadata from Keycloak (`RequireHttpsMetadata: false`).

There is no production configuration and no deployment. Outside Development the API
needs at least `ConnectionStrings__MiniErp` and `Authentication__Jwt__Authority` as
environment variables, or it exits at start (reproduced 2026-11-15).

## 2. Launch profile

`src/MiniErp.Api/Properties/launchSettings.json` has one profile, `http`:

| Setting | Value |
|---|---|
| `applicationUrl` | `http://localhost:5100` |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4317` (the dashboard container) |
| `launchBrowser` | `false` |

`dotnet run --project src/MiniErp.Api` uses it. `--no-launch-profile` drops the whole
profile, including the Development environment.

## 3. Compose stack

`compose.yaml`, project name `mini-erp`. Overrides come from a git-ignored `.env` next
to it; `.env.example` lists them.

| Variable | Default | Used for |
|---|---|---|
| `POSTGRES_DB` | `mini_erp` | database name |
| `POSTGRES_USER` | `mini_erp` | database user |
| `POSTGRES_PASSWORD` | `mini_erp_dev` | database password |
| `POSTGRES_PORT` | `5432` | host port of PostgreSQL |
| `KEYCLOAK_ADMIN` | `admin` | Keycloak admin user |
| `KEYCLOAK_ADMIN_PASSWORD` | `admin` | Keycloak admin password |
| `KEYCLOAK_PORT` | `8080` | host port of Keycloak |

Not overridable (fixed in the file): the dashboard ports `18888` (UI) and `4317` (OTLP),
the image versions, the volume name `postgres-data`.

| Image | Version (2026-11-15) |
|---|---|
| `postgres` | `18.7-alpine` |
| `quay.io/keycloak/keycloak` | `26.8.2` |
| `mcr.microsoft.com/dotnet/aspire-dashboard` | `13.6.5` |

**Trap: `.env` does not reach the API.** The API reads `appsettings.Development.json`,
not `.env`. If you change `POSTGRES_PORT`, `POSTGRES_PASSWORD`, `POSTGRES_USER` or
`POSTGRES_DB`, also set `ConnectionStrings__MiniErp` for the API; if you change
`KEYCLOAK_PORT`, also set `Authentication__Jwt__Authority`. Otherwise the API talks to
the old address.

**Trap: the PostgreSQL image version appears twice.** `compose.yaml` and
`tests/MiniErp.IntegrationTests/ApiFactory.cs` (`new PostgreSqlBuilder("postgres:18.7-alpine")`)
must name the same image, so tests run against the version used locally. Change both in
one commit.

**Keycloak realm** (`deploy/keycloak/realm-mini-erp.json`, imported by
`start-dev --import-realm`):

| In the realm | Value | Must match in code |
|---|---|---|
| Realm | `mini-erp` | `Authentication:Jwt:Authority` |
| Realm roles | `Admin`, `Sales`, `Warehouse`, `Finance` | `src/MiniErp.Platform/Security/Roles.cs` |
| Client `mini-erp-api` | the token audience | `Authentication:Jwt:Audience` |
| Client `mini-erp-dev` | public client, password grant, local use only | the token command in mini-erp-build-and-run §4 |
| Token mappers | `tenant_id` (from a user attribute), `roles`, the audience | `src/MiniErp.Platform/Security/MiniErpClaims.cs` |
| Users | four per tenant, eight in all, password `dev-password` | tenant ids also in `tests/MiniErp.IntegrationTests/TestTokens.cs` |

**Trap: a role or claim renamed in the realm and not in code (or the reverse) makes
every request answer 403.**

To apply an edit of the realm file (Keycloak imports a realm only when it does not
exist yet, and it keeps its data inside the container):

1. `docker compose up -d --force-recreate keycloak` (a recreated container starts empty
   and imports again).
2. Get a fresh token and check `/api/platform/me`.

Not exercised on 2026-11-15.

## 4. Build and tooling settings

| File | Setting | Value | Effect |
|---|---|---|---|
| `global.json` | `sdk.version` / `rollForward` | `10.0.403` / `latestFeature` | any later 10.0 feature band is accepted; CI installs from this file |
| `global.json` | `test.runner` | `Microsoft.Testing.Platform` | `dotnet test` runs xUnit v3 on the new platform; test projects are `OutputType Exe` |
| `Directory.Build.props` | `TargetFramework` | `net10.0` | every project |
| | `Nullable` | `enable` | nullable reference types: `string?` may be null, `string` may not |
| | `ImplicitUsings` | `enable` | common namespaces need no `using` |
| | `TreatWarningsAsErrors` | `true` | a warning fails the build |
| | `AnalysisLevel` | `latest` | newest analyzers |
| `Directory.Packages.props` | `ManagePackageVersionsCentrally` | `true` | versions live only here; project files reference packages without a version |
| `dotnet-tools.json` | `dotnet-ef` | `10.0.15` | local tool, restored with `dotnet tool restore` |
| `.gitattributes` | `* text=auto eol=lf` | | files are stored with LF, whatever the OS |

Package versions on 2026-11-15 (licence in a comment above each one in the file): EF
Core tooling and ASP.NET Core packages `10.0.15`, Npgsql packages `10.0.3`,
`EFCore.NamingConventions` `10.0.1`, OpenTelemetry `1.21.0`, `Scalar.AspNetCore`
`2.21.0`, `Testcontainers.PostgreSql` `4.16.0`, `Shouldly` `4.3.0`, `xunit.v3` `4.0.3`.
Adding or bumping one is gated: mini-erp-change-control §5.

## 5. Constants that act as settings

Changing one of these changes behaviour visible to API clients. Treat it as a feature
change (tests, specification), not as configuration.

| Constant | Where | Value |
|---|---|---|
| Number series and default formats | `Platform/Numbering/NumberSeriesCodes.cs` | `CUST` → `CUST-` + 5 digits, `SO` → `SO-` + 5, `SHIP` → `SHIP-` + 5 |
| Series format limits | `Platform/Numbering/NumberSeries.cs` | digits 3..10, prefix up to 10 characters |
| Permission names | `Platform/Security/Permissions.cs` | `catalog.read`, `catalog.write`, `sales.customers.read`, `sales.customers.write`, `platform.number-series.manage` |
| Role names | `Platform/Security/Roles.cs` | `Admin`, `Sales`, `Warehouse`, `Finance` |
| Claim names | `Platform/Security/MiniErpClaims.cs` | `sub`, `preferred_username`, `tenant_id`, `roles` |
| Paging limits | `ItemEndpoints.cs`, `CustomerEndpoints.cs` | default `take` 50, maximum 100 |
| Field lengths | `Item.cs`, `Customer.cs` | SKU 20, item name 100; customer name 100, address 200, email 254 |
| Schema names | `<Name>DbContext.SchemaName` | `platform`, `catalog`, `sales` |
| Migrations history table | `ModuleDbContext.MigrationsHistoryTable` | `__ef_migrations_history` |
| Telemetry service name | `ObservabilityRegistration.ServiceName` | `mini-erp-api` |

A tenant's number format (prefix, digits) is **data**, not configuration: an Admin
changes it with `PUT /api/platform/number-series/{code}`.

## 6. Test-time configuration

`tests/MiniErp.IntegrationTests/ApiFactory.cs` overrides, for the in-memory API:

- environment `Development` (so migrations run and OpenAPI is served);
- `ConnectionStrings:MiniErp` → the Testcontainers PostgreSQL;
- JWT validation → issuer `https://tests.mini-erp.invalid`, audience `mini-erp-api`, the
  test signing key from `TestTokens`. Keycloak is never contacted.

A test that needs a different setting uses `api.WithWebHostBuilder(b => b.UseSetting(key, value))`,
as `HostingTests` does for the unreachable database.

## 7. Adding a setting: checklist

1. Decide whether it is really configuration (differs per environment) or a constant
   (§5). Prefer a constant until something needs to vary.
2. Name the key `Section:Name`, and declare the name as a `public const string` in the
   registration class it belongs to (as `DatabaseRegistration.MigrateOnStartupSetting`,
   which `Program.cs` reads), so code and tests refer to one name.
3. Give it a safe default in code or in `appsettings.json`. Put a development value in
   `appsettings.Development.json` only if a fresh clone needs it to run.
4. A required setting must fail fast at start-up with a message that says which key to
   set and where (see the two existing checks).
5. Never commit a real secret. Local secrets go in `.env` (git-ignored) with a
   placeholder in `.env.example`.
6. If it affects the compose stack, add `${NAME:-default}` in `compose.yaml` and the
   variable to `.env.example`.
7. Document it: the table in §1 or §3 here, and `CLAUDE.md` if a session needs it to
   run the project.
8. Add a test when the setting changes behaviour (`UseSetting` in a derived factory).

## 8. What phase 2 will add here

Not built; listed so the change is not underestimated. ADR-0004 decides that the
application's database role has no `UPDATE` or `DELETE` on the ledger table and that
migrations run under a separate owner role. That means a second role and connection,
and it touches four places at once: `compose.yaml` (creating the roles),
`ConnectionStrings` (one for the application, one for migrations),
`MigrateModuleDatabasesAsync` (which connection it uses), and
`tests/MiniErp.IntegrationTests/ApiFactory.cs` (the Testcontainers database must have
the same roles, or the test proves nothing). How to do it is undecided.

## When NOT to use this skill

| You need | Use |
|---|---|
| Commands to start, stop, reset, get a token | mini-erp-build-and-run |
| Licence check before adding or bumping a package or image | mini-erp-change-control |
| Adding a permission, a number series or an entity (code, not settings) | mini-erp-add-feature |
| Why the tenant comes only from the token | mini-erp-architecture-contract |

## Provenance and maintenance

Written 2026-11-15 from the files named above. One-line re-verification for each part:

```bash
cat src/MiniErp.Api/appsettings.json src/MiniErp.Api/appsettings.Development.json   # §1
git grep -n "Configuration\[\|GetValue\|GetSection\|GetConnectionString\|IsDevelopment" -- src   # every place code reads configuration
cat src/MiniErp.Api/Properties/launchSettings.json                                  # §2
grep -n "image:\|:-" compose.yaml && cat .env.example                               # §3
git grep -n "postgres:" -- compose.yaml tests                                       # the image must match in both
cat global.json Directory.Build.props dotnet-tools.json                             # §4
grep -n "Version=" Directory.Packages.props                                         # package versions
cat src/MiniErp.Platform/Numbering/NumberSeriesCodes.cs src/MiniErp.Platform/Security/Permissions.cs   # §5
```
