# Architecture Decision Records

Each significant decision gets a short record: the context, the decision, the alternatives that were rejected and why, and the consequences. Records are never rewritten after they are accepted. A changed decision gets a new record that supersedes the old one.

**Status values:** `Proposed` → `Accepted` → (`Superseded by ADR-XXXX` | `Deprecated`)

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith, not microservices | Accepted |
| [0002](0002-postgresql-and-ef-core.md) | PostgreSQL with EF Core | Accepted |
| [0003](0003-multi-tenancy.md) | Shared database, tenant column, query filters | Accepted |
| [0004](0004-append-only-inventory-ledger.md) | Append-only inventory ledger with a balance projection | Accepted |
| [0005](0005-transactional-outbox.md) | Transactional outbox in PostgreSQL | Accepted |
| [0006](0006-keycloak-for-identity.md) | Keycloak as the identity provider | Accepted |
| [0007](0007-free-dependencies-only.md) | Free dependencies only | Accepted |
| [0008](0008-stock-reservation-concurrency.md) | Optimistic concurrency for stock reservation | Accepted |
| [0009](0009-document-numbering.md) | Gapless invoice numbers, gaps allowed elsewhere | Accepted |
| [0010](0010-docker-desktop-for-local-containers.md) | Docker Desktop for local containers (exception to ADR-0007) | Accepted |

## Template

```markdown
# ADR-XXXX: Title

**Status:** Proposed · **Date:** YYYY-MM-DD

## Context
What forces are at play, and why a decision is needed now.

## Decision
What we will do, stated plainly.

## Alternatives considered
- **Option** — why it was rejected.

## Consequences
What becomes easier, what becomes harder, and what we must now watch for.
```
