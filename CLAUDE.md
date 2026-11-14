# CLAUDE.md

Guidance for Claude Code sessions in this repository.

## What this is

**mini-erp** is a multi-tenant ERP backend on .NET: catalog → inventory → sales orders → shipments → invoices → e-invoice submission. It is modelled on how Microsoft Dynamics 365 Business Central posts documents. It is a **portfolio and learning project** for Stefan, not a product. Scope decisions optimise for showing medior-level backend engineering, not for features.

- Specification (scope, domain model, business rules `BR-xx`, phases): [docs/specification.md](docs/specification.md)
- Architecture decisions: [docs/adr/](docs/adr/README.md)

**Status (2026-11-14):** phase 1 in progress (specification §8). Design documents are merged; ADRs 0001–0010 are `Accepted`. Public repository: https://github.com/stefanbabic5/mini-erp. Work lands through pull requests, squash-merged into `main`.

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
- **Multi-tenancy** ([ADR-0003](docs/adr/0003-multi-tenancy.md)): tenant only from the token's `tenant_id` claim; global query filters; another tenant's id answers `404`.
- **Inventory ledger** ([ADR-0004](docs/adr/0004-append-only-inventory-ledger.md)): append-only entries plus a balance projection updated in the same transaction.
- **Transactional outbox** ([ADR-0005](docs/adr/0005-transactional-outbox.md)) for e-invoice submission; at-least-once delivery, idempotent handlers.
- **Keycloak** ([ADR-0006](docs/adr/0006-keycloak-for-identity.md)) as OIDC provider; tests sign their own tokens.
- **Stock reservation** uses optimistic concurrency with a retry of the whole unit of work ([ADR-0008](docs/adr/0008-stock-reservation-concurrency.md)). **Invoice numbers** are gapless through a row lock on the number series ([ADR-0009](docs/adr/0009-document-numbering.md)).

## Prerequisites

- .NET SDK 10
- Docker with Linux containers (Docker Desktop with the WSL 2 backend on Windows; see [ADR-0010](docs/adr/0010-docker-desktop-for-local-containers.md))

## Commands

None yet. Phase 1 adds build, test, `docker compose` and migration commands here.

## Skill library

Not created yet, on purpose. A `.claude/skills/` library will be generated at the end of phase 1, when there is code, tests and git history to ground it in. Until then this file is the only project guide.
