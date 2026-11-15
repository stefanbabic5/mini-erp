---
name: mini-erp-build-and-run
description: >
  Runbook for building, running and operating mini-erp locally. Load this when
  setting up a fresh clone; when dotnet build, dotnet test, dotnet run, dotnet ef or
  docker compose fails or behaves oddly; when starting or stopping PostgreSQL,
  Keycloak or the telemetry dashboard; when you need an access token for a demo user
  (alfa.sales, beta.admin, ...) or want to call the API with curl or Scalar; when CI
  failed and must be reproduced locally; when repeating the fresh-clone check; when
  looking at the local database with psql; when resetting the database; when asked
  "is the API running", "why is /health/ready 503", "why 401/403/404"; or when
  checking what CI runs. Covers prerequisites, the first-run sequence, every local
  URL and port, the demo users, command anatomy, and the traps verified in this
  repository. Settings and their defaults live in mini-erp-config-and-flags; creating
  migrations lives in mini-erp-add-feature.
---

# mini-erp build and run

Everything needed to get **mini-erp** running on a developer machine and to operate it
day to day. Commands were run in this repository on 2026-11-15 (Windows 11, .NET SDK
10.0.403, Docker 29.9.3), **except**: `git clone`, `docker compose up -d` / `down` /
`down -v` (the stack was already running and was left alone), `gh pr checks`, the
fresh-clone check in §2, and the rows marked "not reproduced".

Run all commands from the repository root (the folder with `MiniErp.slnx`).

Terms, defined once:

- **Compose stack**: the three containers in `compose.yaml` (PostgreSQL, Keycloak,
  dashboard). The API is **not** in it; the API runs on the host with `dotnet run`.
- **Testcontainers**: a library that starts a throwaway PostgreSQL container for the
  test run. Tests do not use the compose stack.
- **Migration**: a generated C# class that changes the database schema. Business Central
  comparison: the schema sync that happens when an extension is published, written as
  code.
- **Scalar**: the web UI that shows the API's OpenAPI document and can send requests.

## 1. Prerequisites

| Need | Version | Check |
|---|---|---|
| .NET SDK | 10.0.403 or a later 10.0 feature band (`global.json`, `rollForward: latestFeature`) | `dotnet --version` |
| Docker with **Linux** containers | any recent engine; on Windows, Docker Desktop with the WSL 2 backend ([ADR-0010](../../../docs/adr/0010-docker-desktop-for-local-containers.md)) | `docker version` |
| GitHub CLI (only for PR work) | any | `gh --version` |

Nothing else: no global tools, no accounts, no secrets.

## 2. First run from a fresh clone

```bash
git clone https://github.com/stefanbabic5/mini-erp.git
```
```bash
cd mini-erp
```
```bash
docker compose up -d
```
```bash
dotnet build
```
```bash
dotnet test
```
```bash
dotnet run --project src/MiniErp.Api
```

Expected: build ends with `0 Warning(s) 0 Error(s)`; the test summary shows
`failed: 0` (121 tests on 2026-11-15; trust the output, not this number); the API
listens on http://localhost:5100 and on first start creates the schemas `platform`,
`catalog` and `sales`.

`dotnet run` keeps the terminal. Stop it with Ctrl+C.

**The fresh-clone check** (what backs the README's "a fresh clone runs"; done for PR #9
on 2026-11-14, not repeated on 2026-11-15). Repeat it after any change to
`compose.yaml`, the realm file or the README's quick start:

1. Stop the regular stack so its ports are free: `docker compose stop`.
2. Clone into an empty folder outside this one.
3. In the clone: `docker compose -p mini-erp-fresh up -d` (its own project name, so its
   own empty volume), then `dotnet run --project src/MiniErp.Api`.
4. Check: `/health/ready` answers 200; the README's token command, copied as written,
   returns a token; `/api/platform/me` shows `alfa.sales`; `dotnet test` passes.
5. Remove it: `docker compose -p mini-erp-fresh down -v`, delete the folder, then
   `docker compose start` in the regular checkout.

## 3. What runs where

| What | URL / port | Started by | Notes |
|---|---|---|---|
| API | http://localhost:5100 | `dotnet run --project src/MiniErp.Api` | launch profile `http`, environment `Development` |
| API reference (Scalar) | http://localhost:5100/scalar | the API, Development only | answers 302 then the UI; paste a token under *Authentication* |
| OpenAPI document | http://localhost:5100/openapi/v1.json | the API, Development only | |
| Liveness / readiness | http://localhost:5100/health/live, `/health/ready` | the API | anonymous; ready is 503 while PostgreSQL is unreachable |
| Who am I | http://localhost:5100/api/platform/me | the API | needs a token; shows user, tenant, roles, permissions |
| PostgreSQL 18.7 | localhost:5432, database `mini_erp` | compose service `postgres` | data in the volume `postgres-data` |
| Keycloak 26.8.2 | http://localhost:8080 | compose service `keycloak` | admin console `admin` / `admin`, realm `mini-erp` |
| Telemetry dashboard | http://localhost:18888 | compose service `dashboard` | logs, traces, metrics; receives OTLP on port 4317 |

Check the stack: `docker compose ps` (three services, state `running`).

## 4. Demo users and tokens

The realm file `deploy/keycloak/realm-mini-erp.json` is imported when Keycloak starts.
Password for every user: `dev-password`. These credentials exist only in the local
realm.

| Tenant | Tenant id | Users |
|---|---|---|
| Alfa | `a1fa0000-0000-0000-0000-000000000001` | `alfa.admin`, `alfa.sales`, `alfa.warehouse`, `alfa.finance` |
| Beta | `b37a0000-0000-0000-0000-000000000002` | `beta.admin`, `beta.sales`, `beta.warehouse`, `beta.finance` |

The user name's suffix is its role (Admin, Sales, Warehouse, Finance). What each role
may do: specification §2.

Get a token and call the API (bash; on Windows use Git Bash):

```bash
TOKEN=$(curl -s http://localhost:8080/realms/mini-erp/protocol/openid-connect/token \
  -d grant_type=password -d client_id=mini-erp-dev -d username=alfa.sales -d password=dev-password \
  | sed -E 's/.*"access_token":"([^"]+)".*/\1/')

curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5100/api/platform/me
```

Expected for `alfa.sales`: `"roles":["Sales"]` and the permissions `catalog.read`,
`sales.customers.read`, `sales.customers.write`.

Reading a response code:

| Code | Meaning here | First thing to check |
|---|---|---|
| 401 | no token, expired token, wrong audience or signing key | get a fresh token; is Keycloak up? |
| 403 | the token has no valid `tenant_id`, or the role lacks the permission | `/api/platform/me`; specification §2 |
| 404 | wrong route, or the id belongs to another tenant (BR-13, by design) | are you using the tenant that created the row? |
| 412 | `If-Match` does not match the current version | `GET` again, use the new `ETag` |
| 428 | `PUT` without `If-Match` | send the `ETag` from the last `GET` |
| 503 on `/health/ready` | PostgreSQL unreachable | `docker compose ps` |

## 5. Command anatomy

| Task | Command |
|---|---|
| Start / stop the stack | `docker compose up -d` / `docker compose down` |
| Stop and **delete the database volume** | `docker compose down -v` (destroys local data; Keycloak re-imports its realm on the next start) |
| Build | `dotnet build` (CI uses `--configuration Release`) |
| All tests | `dotnet test` |
| One test project | `dotnet test --project tests/MiniErp.ArchitectureTests` |
| Tests by name | `dotnet test --project tests/MiniErp.IntegrationTests --filter-method "*BR13*"` |
| Tests by class | `dotnet test --project tests/MiniErp.IntegrationTests --filter-class "*ItemEndpointTests"` |
| Tests by VSTest-style expression | `dotnet test --project tests/MiniErp.ArchitectureTests --filter "FullyQualifiedName~Platform_does"` |
| List tests | `dotnet test --no-build --list-tests` (lists 112 on 2026-11-15 while a run reports 121: the two validation theories whose data rows are anonymous objects are listed once) |
| Is the API up? | `curl -s -o /dev/null -w "%{http_code}" http://localhost:5100/health/ready` (200 = ready, 503 = no database, 000 = not running) |
| Run the API | `dotnet run --project src/MiniErp.Api` |
| Restore the local tools (`dotnet-ef`) | `dotnet tool restore` |
| List a module's migrations | `dotnet ef migrations list --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext` |
| Model and migrations in sync? | `dotnet ef migrations has-pending-model-changes --project src/Modules/MiniErp.Catalog --startup-project src/MiniErp.Api --context CatalogDbContext` |
| SQL prompt | `docker compose exec postgres psql -U mini_erp -d mini_erp` |
| What CI did on a PR | `gh pr checks <number>` |

For Sales use `src/Modules/MiniErp.Sales` and `SalesDbContext`; for Platform use
`--project src/MiniErp.Platform --context PlatformDbContext`.

Inside `psql`: `\dn` lists schemas (expect `catalog`, `platform`, `sales`, `public`),
`\dt catalog.*` lists a module's tables, `SELECT * FROM platform.number_series;` shows
the counters.

**CI** (`.github/workflows/ci.yml`, job `build-and-test`, ubuntu-latest) runs exactly:
`dotnet restore`, `dotnet build --configuration Release --no-restore`,
`dotnet test --configuration Release --no-build`. Reproduce a CI failure locally with
those three commands.

## 6. Where output lands

- **Console of `dotnet run`**: application logs, including every SQL command.
- **Dashboard** (http://localhost:18888): the same logs plus traces and metrics, but
  only when the API was started with the `http` launch profile, which sets
  `OTEL_EXPORTER_OTLP_ENDPOINT`. Tests never export telemetry.
- **Schema**: applied by the API at start-up in Development (`Database:MigrateOnStartup`).
  Each module records its applied migrations in `<schema>.__ef_migrations_history`.
- **Build output**: `bin/` and `obj/` under each project, git-ignored.

## 7. Traps

| Symptom | Cause | Do |
|---|---|---|
| `dotnet test` fails at once in every integration test, with a Docker/Testcontainers connection error | Docker is not running, or is switched to Windows containers | Start Docker Desktop; switch to Linux containers; rerun |
| `dotnet ef migrations list` prints "An error occurred using the connection to database 'mini_erp_design_time'" | The design-time factory uses a connection string that is never meant to connect (`DesignTimeDbContextOptions`) | Harmless. The migration names are still listed; only the applied/pending status is missing |
| `dotnet restore` or `dotnet build` fails with `error NU1901`-`NU1904: Warning As Error: Package '<name>' <version> has a known ... severity vulnerability`, locally or in CI, with no change in the repository | NuGet Audit found a known vulnerability in a package (direct or transitive) on restore; a newly published advisory can turn a green build red overnight. `NU1900` means the vulnerability data could not be downloaded | Find the path with `dotnet nuget why <project> <package>`; upgrade the package closest to the direct reference (a change through mini-erp-change-control). Do not suppress it without Stefan's agreement |
| `grep -r` finds matches in `.dll` files | `bin/` and `obj/` are inside `src/` and `tests/` | Use `git grep -n "<text>" -- src tests` |
| A warning fails the build | `TreatWarningsAsErrors` is on for every project (`Directory.Build.props`) | Fix the warning; do not suppress it |
| The API exits at start with "Connection string 'MiniErp' is missing" (reproduced with `ASPNETCORE_ENVIRONMENT=Production` and `--no-launch-profile`) | It was started outside the Development environment, where `appsettings.Development.json` is not read | Use `dotnet run --project src/MiniErp.Api`; see mini-erp-config-and-flags |
| The API exits at start with a PostgreSQL connection error | Migrations run at start-up in Development and PostgreSQL is not up | `docker compose up -d`, wait for `postgres` to be healthy |
| Every request answers 401 right after `docker compose up -d` | Keycloak is still starting (it is the slowest container), so the API cannot fetch its signing keys | Wait until `curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/realms/mini-erp` prints 200. Not reproduced on 2026-11-15 (Keycloak was already up); treat as the first suspect, not a fact |
| A customer number is not `CUST-00001` on a database that has been used | The tenant's series already advanced, or an Admin changed its format | Expected; numbers have gaps by design (ADR-0009). `docker compose down -v` starts clean |
| `dotnet build` cannot overwrite a `.dll` (Windows) | The API from an earlier `dotnet run` is still running and holds its files | Stop it (Ctrl+C in its terminal) and build again. Not reproduced on 2026-11-15; standard Windows behaviour |

Tests and the compose stack are independent: tests start their own PostgreSQL
(`postgres:18.7-alpine`, the same image as `compose.yaml`) and sign their own tokens, so
they pass with the compose stack stopped, and they never touch the `mini_erp` database.

## 8. Operations that need Stefan's go-ahead

- `docker compose down -v` when the local database holds data he may want.
- Anything that changes `compose.yaml`, the realm file, an image version or
  `global.json`: that is a change, see mini-erp-change-control.

## When NOT to use this skill

| You need | Use |
|---|---|
| What a setting does, its default, how to add one | mini-erp-config-and-flags |
| Creating a migration, an entity or an endpoint | mini-erp-add-feature |
| Writing tests, what a failing test means | mini-erp-testing-and-qa |
| Branch, commit and PR rules | mini-erp-change-control |

## Provenance and maintenance

Written 2026-11-15. Verified that day by running: build, all tests, the test filters,
`dotnet tool restore`, the `dotnet ef` commands, the API start with `/health/ready`,
the token command, `/api/platform/me`, `/scalar` and `psql \dn`. Re-verify what may
drift:

```bash
cat global.json                                        # SDK version, test runner
grep -n "image:" compose.yaml                          # container versions
grep -n "applicationUrl" src/MiniErp.Api/Properties/launchSettings.json   # API port
grep -n '"username"' deploy/keycloak/realm-mini-erp.json                  # demo users
cat .github/workflows/ci.yml                           # CI commands
dotnet test --no-build --list-tests | tail -1          # listed tests (lower than the run total, see §5)
```
