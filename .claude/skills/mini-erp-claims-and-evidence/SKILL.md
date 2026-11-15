---
name: mini-erp-claims-and-evidence
description: >
  The project rule "evidence over claims" for mini-erp: what the README, the
  specification, CLAUDE.md, a skill, a PR description or the GitHub repository
  description may claim, and which test, measurement or code proves each claim.
  Load this BEFORE editing the README's status note, "What is built in", "What works
  today", "Tests" or "Roadmap" sections; before writing that something works, is
  proven, is correct under concurrency or is isolated per tenant; before raising any
  number (test counts, parallel requests, ADR count); when a feature lands and a
  document should say so; when asked "can we say X", "is X true yet", "what does
  this project prove", "what is not built yet"; and when reviewing any text for
  overstatement. Covers the rule for a claim, the claim-to-proof table with the
  command that reproduces each proof, claims not allowed yet and what would unlock
  them, and how to word a claim. Document mechanics: mini-erp-docs-and-writing. Writing
  the tests that create proof: mini-erp-testing-and-qa.
---

# mini-erp claims and evidence

`CLAUDE.md` working agreement 4, "Evidence over claims", says: "The README, the
specification and every other document may only claim what a test, a measurement or
the code proves; something decided but not built is labelled as such." The README
repeats it: "This README only claims what the code and its tests show." This skill is
the ledger behind those sentences.

A reader who finds one claim that does not hold stops trusting the rest of the
documents. An honest "not built yet, here is the decision record" is worth more than
an overstated feature, and it keeps the next session from building on something that
is not there.

Terms, defined once:

- **Claim**: any statement about what the project does or proves, in the README, the
  specification, `CLAUDE.md`, a skill, a PR description, a commit message or the
  GitHub repository description.
- **Proof**: a test that fails when the claim is false, a measurement with its command
  and date, or code that can be pointed to.
- **Decided, not built**: there is an accepted ADR or a specification section, and no
  code.

## 1. The rule for a claim

A claim may be made when all four hold:

1. **It names something that exists on `main`**, not on a branch and not in the
   specification's later phases.
2. **It has a proof you can run or open today.** For behaviour: a named test. For
   "correct under concurrency": a parallel test on real PostgreSQL. For performance:
   a measurement (none exists yet). For structure: a file or a test over the assemblies.
3. **Its scope matches the proof.** "Customer numbers are unique under 50 parallel
   requests" is proven; "numbering is correct under concurrency" for invoices is not.
4. **Its numbers come from a run**, with the date, or sit beside the command that
   reproduces them.

If a claim fails one of these, either narrow it until it passes, or label it:
"planned (phase 2)", "decided in ADR-0008, not built yet".

**The GitHub repository description is a claim too.** It is the first sentence a
visitor reads, and nothing in the repository checks it. On 2026-11-15 it reads:
"Multi-tenant ERP backend on .NET 10 and PostgreSQL that rebuilds how Business
Central posts documents. Phase 1 of 4 done: tenancy, auth, number series, items,
customers." Update it in the same step that marks a phase done in specification §8
and the README (changing it is a repository setting: Stefan's go-ahead first), and
never let it name something that is only decided. Read it with
`gh api repos/stefanbabic5/mini-erp --jq .description`.

## 2. Claims that hold today, with their proof

Verified on 2026-11-15 (`dotnet test`: 121 passed). Each proof can be rerun with
`dotnet test --project tests/MiniErp.IntegrationTests --filter-method "*<name>*"`
unless another command is given.

| Claim (README wording or equivalent) | Proof | Scope limit to keep in the wording |
|---|---|---|
| The tenant comes only from the access token | `HttpTenantContext` in `src/MiniErp.Platform/Security/TenantContext.cs`; `Token_without_tenant_answers_403`, `Token_with_invalid_tenant_answers_403` | |
| A query filter adds the tenant to every query | `TenantIsolationTests.BR13_queries_return_only_the_current_tenants_rows` | applies to entities marked `ITenantOwned`; raw SQL must filter itself |
| Writes to another tenant's rows are refused | `BR13_inserting_a_row_for_another_tenant_is_refused`, `BR13_changing_another_tenants_row_is_refused_even_past_the_filter`, `BR13_deleting_another_tenants_row_is_refused_even_past_the_filter` | |
| Another tenant's item or customer answers 404 on every endpoint | `BR13_another_tenants_item_is_not_found_on_any_endpoint`, `BR13_another_tenants_customer_is_not_found_on_any_endpoint` | say "item and customer endpoints"; see §3 for "every endpoint" |
| Secure by default: an endpoint needs an authenticated user with a tenant unless it opts out | `FallbackPolicy` in `SecurityRegistration.cs`; `Request_without_token_answers_401_as_problem_details` | |
| Endpoints require permissions; roles map to permissions in one place | `RolePermissions.cs`; `PermissionMatrixTests.Role_grants_exactly_the_permissions_in_the_matrix` (every role and permission pair) | |
| Tokens with a wrong audience, an untrusted signing key or an expired lifetime are refused | `Token_for_another_audience_answers_401`, `Token_signed_with_an_untrusted_key_answers_401`, `Expired_token_answers_401` | do not add "issuer": the framework validates it, but no test sends a wrong issuer. Tests use a test issuer and key, not Keycloak |
| No lost updates: a stale version answers 412 | `Update_with_a_stale_etag_answers_412_and_keeps_the_other_change`, `Update_with_a_stale_etag_answers_412_and_without_one_428` | `PUT` on items and customers; block and unblock are not version-checked |
| 50 parallel requests get 50 distinct, consecutive numbers | `NumberSeriesTests.BR09_parallel_requests_never_get_the_same_number` | series where gaps are allowed (CUST, SO, SHIP); not invoice numbers |
| Module boundaries are enforced by tests | `dotnet test --project tests/MiniErp.ArchitectureTests` (9 tests) | assembly references and public types; not SQL across schemas |
| Integration tests run the real API against real PostgreSQL in Docker | `tests/MiniErp.IntegrationTests/ApiFactory.cs` (Testcontainers, `WebApplicationFactory`) | |
| CI needs nothing but Docker; tests sign their own tokens | `.github/workflows/ci.yml`; `TestTokens.cs` | |
| PIB check digit and registration number are validated (BR-15) | `BR15_valid_pib_is_accepted`, `BR15_invalid_pib_answers_400`, `BR15_registration_number_must_be_8_digits` | |
| SKU and PIB are unique per tenant | `Duplicate_sku_in_the_same_tenant_answers_409`, `BR13_the_same_sku_in_another_tenant_is_allowed`, `Duplicate_pib_in_the_same_tenant_answers_409`, `BR13_the_same_pib_in_another_tenant_is_allowed_and_numbered_from_1` | |
| Items and customers are blocked, never deleted | `BR12_items_cannot_be_deleted`, `Customers_cannot_be_deleted`, `BR11_block_and_unblock_change_the_flag` | the blocked flag has no effect yet: there are no orders |
| Errors are RFC 9457 Problem Details with a stable `code` for conflicts | `Unknown_route_answers_404_as_problem_details`; the 409 tests read `code` | two codes exist: `catalog.duplicate_sku`, `sales.duplicate_tax_id` |
| Readiness fails while PostgreSQL is unreachable, liveness stays up | `Readiness_probe_answers_503_when_postgres_is_unreachable_while_liveness_stays_healthy` | |
| One-command local start; a fresh clone runs | PR #9 describes the fresh-clone check done on 2026-11-14 | a manual check, not automated; repeat it (mini-erp-build-and-run §2) before repeating the claim after changes to compose or the quick start |
| Ten ADRs, each with rejected alternatives | `ls docs/adr/` | ADR-0004, ADR-0005 and ADR-0008 describe things not built yet, and ADR-0009 is half built (§3) |
| Free dependencies only, every package, image and tool with its licence listed | `Directory.Packages.props`, the comments in `compose.yaml`, `CLAUDE.md` "Repository layout" for `dotnet-ef` | Docker Desktop is a recorded exception (ADR-0010) |
| Dependencies, transitive ones included, are checked for known vulnerabilities, and a finding fails the build locally and in CI | NuGet Audit defaults for `net10.0` plus `TreatWarningsAsErrors`; checked 2026-11-15 with two throwaway projects (a direct and a transitive vulnerable `Newtonsoft.Json`) that failed restore and `build --no-restore -c Release` with `error NU1903` | the CI half follows from CI running the same `dotnet restore` with the same SDK and settings; it was not observed on a GitHub runner. Repeat the probe after changing `global.json`, `Directory.Build.props` or any `NuGetAudit*` / `WarningsNotAsErrors` setting |
| 121 tests: 9 architecture, 112 integration | `dotnet test` on 2026-11-15 | quote the run total, not the `--list-tests` count (112; why: mini-erp-build-and-run §5). Current totals are owned by mini-erp-testing-and-qa |
| OpenTelemetry logs, traces and metrics to a local dashboard | `ObservabilityRegistration.cs`; PR #4's manual check | no automated test; logs do not carry tenant or user id |

## 3. Claims that are not allowed yet

What is decided but not built is tracked in mini-erp-architecture-contract §6; when a
row there gets code and a proof, the matching row here moves to §2. "Goal G2, G3, G4"
are the goals in specification §1.

| Do not say | Why not | What unlocks it |
|---|---|---|
| "Two orders racing for the last unit: exactly one wins" / "stock never goes negative under concurrency" (BR-02, goal G2) | no inventory, no orders, no reservation code | phase 2: the parallel release test described in ADR-0008 and specification §8 |
| "Append-only inventory ledger" (BR-01) | ADR-0004 only | phase 2: the ledger, the restricted database role, the reconciliation test |
| "Gapless invoice numbers" | ADR-0009 only; today's series allow gaps | phase 3: the parallel posting test |
| "Transactional outbox", "resilient integration with a flaky external service", "at-least-once delivery" (goals G3, G4) | ADR-0005 only | phase 3 acceptance test with the simulator |
| "Idempotent POSTs" (BR-14) | not built | phase 2 |
| "Handles N requests per second", any latency or throughput number | no load test has been run | phase 4: k6 run with the numbers and the command published |
| "Row-level security" | a phase 4 stretch item | policies plus a test with raw SQL |
| "Every endpoint has a 404 cross-tenant test" (as an absolute) | number-series endpoints are keyed by code and are covered by a different test (`BR13_one_tenants_change_is_invisible_to_another_tenant`); `/api/platform/me` has no id | either say "every endpoint that takes an id", or word it per feature (the README does since 2026-11-15) |
| "Unit and integration tests" | there is no unit test project (specification §9 marks it planned) | a unit test project |
| "Structured logs with tenant and user id" | the specification asks for it and marks it not in place yet | the log scope |
| "Production-ready", "deployed", "used by" | it runs locally only; no hosting, no users | not a goal before phase 4, and then only what is true |
| "Complete ERP", "invoicing", "sales orders" as existing features | Inventory and Invoicing are empty modules; Sales has customers only | the phase that builds them |
| "A Business Central clone/replacement" | it is modelled on how BC posts documents; it is not compatible with BC | never; say "modelled on" |

## 4. Wording a claim

| Instead of | Write | Why |
|---|---|---|
| "Fully tested" | "121 tests, run on every pull request" (with the date or the command) | a number that can be checked |
| "Thread-safe numbering" | "a test fires 50 parallel requests and gets exactly 50 distinct, consecutive numbers" | states the proof |
| "Enterprise-grade multi-tenancy" | "tenant isolation by an EF Core query filter plus a SaveChanges guard, with a cross-tenant test per feature" | names the mechanism and the proof |
| "Implements optimistic concurrency" (for stock) | "optimistic concurrency on item and customer updates via ETag / If-Match; the same approach is decided for stock reservation (ADR-0008), not built yet" | separates built from decided |
| "Uses microservices best practices" | "a modular monolith; architecture tests fail the build when a module references another" | accurate, and the ADR explains why not microservices |

Rules of thumb:

- Prefer the mechanism and the proof to an adjective.
- Use "decided (ADR-NNNN), not built yet" freely: pointing to the decision record is
  a legitimate statement about the design.
- A roadmap item is written as scope with a status (`next`, `planned`), never in the
  present tense.
- Counts go stale. After any change to tests, rerun and update the README's total and
  its per-project table together.

## 5. Before publishing a claim: checklist

- [ ] The thing is on `main` (`git log main --oneline -5`, not only on a branch).
- [ ] I can name the test, file or measurement, and I ran or opened it today.
- [ ] The wording does not reach past the proof's scope (§2, last column).
- [ ] Numbers come from today's run.
- [ ] Nothing from §3 slipped in through a summary sentence or the roadmap's tense.
- [ ] If the README changed, Stefan has seen the diff (mini-erp-change-control §1, rule 2).

## When NOT to use this skill

| You need | Use |
|---|---|
| The README's structure, house style, ADR template | mini-erp-docs-and-writing |
| To create the proof (write the test) | mini-erp-testing-and-qa |
| The design decisions behind a claim | mini-erp-architecture-contract |
| To run the tests or the fresh-clone check | mini-erp-build-and-run §5 and §2 |

## Provenance and maintenance

Written 2026-11-15 against `main` at the end of phase 1 (PRs #1-#9), after a green run
of 121 tests; renamed from `mini-erp-portfolio-claims` and refocused on the project rule
the same day. This skill goes stale faster than any other: every merged feature moves
rows from §3 to §2. Re-verify:

```bash
dotnet test                                              # the totals in §2
dotnet test --no-build --list-tests | grep -c "BR13"     # cross-tenant proofs still present
ls src/Modules/MiniErp.Inventory src/Modules/MiniErp.Invoicing   # more than the module class? then revisit §3
ls docs/adr/ | wc -l                                     # ADR count (10 records + README on 2026-11-15)
grep -n "claims what the code" README.md                 # the README's own promise is still there
dotnet msbuild src/MiniErp.Api/MiniErp.Api.csproj -getProperty:NuGetAuditMode -getProperty:TreatWarningsAsErrors   # all / true, or the audit row in §2 no longer holds
gh api repos/stefanbabic5/mini-erp --jq .description     # the repository description (§1, last paragraph)
```
