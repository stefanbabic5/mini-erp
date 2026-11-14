# mini-erp

A multi-tenant ERP backend on .NET 10: **catalog → inventory → sales orders → shipments → invoices → e-invoice submission**, modelled on how Microsoft Dynamics 365 Business Central posts documents and rebuilt from scratch.

> **Status:** design phase. The specification and architecture decisions are done; code starts with phase 1. This README will grow with the code, and it only claims what the code and tests prove.

## What this project is about

A portfolio project focused on the backend problems that are hard to get right:

- **Correctness under concurrency:** two orders racing for the last unit in stock; exactly one wins, proven by a parallel test on real PostgreSQL.
- **Transactional integrity:** posting is all-or-nothing, with an append-only inventory ledger like Business Central's Item Ledger Entries.
- **Gapless invoice numbering** under parallel posting.
- **Resilience:** e-invoices leave through a transactional outbox to a deliberately flaky external service.
- **Tenant isolation:** several companies on one instance, with a cross-tenant test on every endpoint.

## Documentation

| Document | Contents |
|---|---|
| [Specification](docs/specification.md) | Scope, domain model, business rules (`BR-01`…), order lifecycle, API conventions, phases |
| [Architecture decisions](docs/adr/README.md) | Nine ADRs: modular monolith, PostgreSQL + EF Core, multi-tenancy, ledger, outbox, Keycloak, free dependencies only, concurrency, numbering |

## Planned stack

.NET 10 · ASP.NET Core minimal APIs · EF Core + PostgreSQL · Keycloak (OIDC) · OpenTelemetry · xUnit + Testcontainers · Docker Compose · GitHub Actions. Everything is free and open source ([ADR-0007](docs/adr/0007-free-dependencies-only.md)).

## License

[MIT](LICENSE)
