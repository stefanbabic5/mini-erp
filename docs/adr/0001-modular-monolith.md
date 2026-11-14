# ADR-0001: Modular monolith, not microservices

**Status:** Accepted · **Date:** 2026-11-14

## Context
The system has four business modules (Catalog, Inventory, Sales, Invoicing) that interact closely: releasing an order reserves stock, and posting an invoice reads shipped quantities. One developer builds it, it runs locally, and it has no independent scaling needs. The modules should still be separable later, and their boundaries should be visible in the code.

## Decision
One deployable ASP.NET Core application, split into modules:

- Each module owns its tables in its own PostgreSQL schema (`catalog`, `inventory`, `sales`, `invoicing`, `platform`) and its own `DbContext`.
- A module exposes a small public contract (interfaces and DTOs). Other modules never reference its internals or query its tables.
- Synchronous calls between modules go through those contracts in-process. Side effects that can happen later go through the outbox ([ADR-0005](0005-transactional-outbox.md)).
- Architecture tests in CI fail the build when a module references another module's internals.

Operations that must be atomic across modules, such as posting a shipment (sales document + inventory ledger), share one database transaction.

## Alternatives considered
- **Microservices**: independent deployment and scaling that nobody needs here. It would add network failure modes, distributed transactions or sagas for posting, and several deployables to run locally, for no benefit at this size.
- **A layered monolith with no module boundaries** (one `DbContext`, any code queries any table): fastest to start, but the boundaries would erode, and it shows less design skill.

## Consequences
- Posting can use one ACID transaction, so correctness is simpler than with sagas.
- Boundaries hold only while the architecture tests run in CI.
- Splitting a module out later means replacing its in-process contract with an HTTP or message interface. Its tables already live in their own schema, so the data split is clean.
- Cross-module transactions are a deliberate coupling. They must be listed in the module's documentation, because they are what would need a saga after a split.
