---
name: mini-erp-portfolio-claims
description: >
  What may be claimed about mini-erp in public, and the proof behind each claim. Load
  this BEFORE editing the README's status note, "What it shows", "What works today",
  "Tests" or "Roadmap" sections; before writing a CV bullet, LinkedIn text, a cover
  letter line, an interview answer or a repository description about this project;
  before raising any number (test counts, parallel requests, ADR count); when a
  feature lands and you want to say so; when asked "can we say X", "is X true yet",
  "what does this project prove", "what should I not claim"; and when reviewing text
  for overstatement. Covers the rule for a claim, the claim-to-proof table with the
  command that reproduces each proof, claims that are not allowed yet and what would
  unlock them, how to word a claim honestly, and CV-ready statements that are true on
  the date written. Not for document mechanics (mini-erp-docs-and-writing) or for
  writing the tests that create proof (mini-erp-testing-and-qa).
---

# mini-erp portfolio claims

**mini-erp** exists to show backend engineering at medior level to someone who may
check. Its README says: "This README only claims what the code and its tests show."
`CLAUDE.md` makes that a rule: "The README may only claim what a test, a measurement or
the code proves" (working agreement 4). This skill is the ledger behind that sentence.

An interviewer who finds one claim that does not hold stops trusting the rest. An
honest "not built yet, here is the decision record" is worth more than an overstated
feature.

Terms, defined once:

- **Claim**: any statement about what the project does or shows, in the README, a CV,
  a profile or a conversation.
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
| Free dependencies only, every package with its licence listed | `Directory.Packages.props` | Docker Desktop is a recorded exception (ADR-0010) |
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
| "Every endpoint has a 404 cross-tenant test" (as an absolute) | number-series endpoints are keyed by code and are covered by a different test (`BR13_one_tenants_change_is_invisible_to_another_tenant`); `/api/platform/me` has no id | either say "every endpoint that takes an id", or word it per feature |
| "Unit and integration tests" | there is no unit test project | a unit test project |
| "Dependency scanning in CI", "structured logs with tenant and user id" | the specification asks for them; neither exists | the CI step; the log scope |
| "Production-ready", "deployed", "used by" | it runs locally only; no hosting, no users | not a goal before phase 4, and then only what is true |
| "Complete ERP", "invoicing", "sales orders" as existing features | Inventory and Invoicing are empty modules; Sales has customers only | the phase that builds them |
| "A Business Central clone/replacement" | it is modelled on how BC posts documents; it is not compatible with BC | never; say "modelled on" |

Note that the README's current sentence "Every endpoint has a test that tries to reach
another tenant's data and gets 404" is the absolute form in the row above. Tightening
it is a docs change for Stefan to approve.

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
- Use "decided (ADR-NNNN), not built yet" freely: showing the decision record is a
  legitimate claim about design skill.
- A roadmap item is written as scope with a status (`next`, `planned`), never in the
  present tense.
- Counts go stale. After any change to tests, rerun and update the README's total and
  its per-project table together.

## 5. Statements that are safe on a CV (2026-11-15)

Each is true today and maps to §2. Recheck before reuse; the phase may have moved on.

Choose verbs you can defend in an interview. This repository is openly developed with
an AI assistant (its `CLAUDE.md` is public and says how), with every change reviewed
and merged by the maintainer. Whether a bullet says "built", "designed" or "designed
and reviewed" is the maintainer's call; no bullet may imply something a reader of the
repository would find untrue.

- Built the foundation of a multi-tenant ERP backend on .NET 10 and PostgreSQL as a
  modular monolith (Catalog and Sales implemented, Inventory and Invoicing scaffolded),
  with module boundaries enforced by architecture tests in CI.
- Implemented tenant isolation with EF Core global query filters and a `SaveChanges`
  interceptor; every item and customer endpoint has a test proving another tenant's
  data answers 404.
- Secured the API with Keycloak (OIDC/JWT), permission-based authorization policies
  and a deny-by-default fallback policy; the role-to-permission matrix is verified by
  tests.
- Prevented lost updates with ETag / If-Match over PostgreSQL's `xmin` row version.
- Wrote 121 automated tests, most of them integration tests running the real API
  against PostgreSQL in Docker (Testcontainers), on every pull request in GitHub
  Actions.
- Proved that the number series (customer numbers today) never hands out a duplicate,
  with a test of 50 parallel requests.
- Recorded ten architecture decisions (ADRs), each with the alternatives rejected,
  including a free-dependencies-only policy.

Not yet safe on a CV: anything in §3.

## 6. Before publishing a claim: checklist

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
of 121 tests. This skill goes stale faster than any other: every merged feature moves
rows from §3 to §2. Re-verify:

```bash
dotnet test                                              # the totals in §2 and §5
dotnet test --no-build --list-tests | grep -c "BR13"     # cross-tenant proofs still present
ls src/Modules/MiniErp.Inventory src/Modules/MiniErp.Invoicing   # more than the module class? then revisit §3
ls docs/adr/ | wc -l                                     # ADR count (10 records + README on 2026-11-15)
grep -n "claims what the code" README.md                 # the README's own promise is still there
cat .github/workflows/ci.yml                             # dependency scan added? then update §3
```
