# ADR-0003: Shared database, tenant column, query filters

**Status:** Accepted · **Date:** 2026-11-14

## Context
Several companies (tenants) use one instance. Data leaking between tenants is the worst possible bug in this system (BR-13). The isolation model must be cheap to run locally and hard to get wrong in new code.

## Decision
- **One database, one set of tables, a `TenantId` column on every tenant-scoped row**, included in every unique index (e.g. `(TenantId, Sku)`).
- The tenant comes **only from the access token** (`tenant_id` claim). Never from a header, route or body.
- A scoped `ITenantContext` holds the current tenant. EF Core **global query filters** add `TenantId = current` to every query, and a `SaveChanges` interceptor stamps `TenantId` on new rows and refuses changes to rows of another tenant.
- Another tenant's id answers `404`, never `403`.
- **Tests:** every tenant-scoped endpoint has a cross-tenant test (read, update and action, with a token of tenant B on data of tenant A).
- **Defence in depth (phase 4, stretch):** PostgreSQL row-level security policies using a per-transaction `app.tenant_id` setting, so that even a raw SQL query that forgets the filter returns nothing.

## Alternatives considered
- **A database per tenant**: the strongest isolation, but migrations, connections and seeding multiply per tenant. Too heavy for this project.
- **A schema per tenant**: middle ground with the same migration overhead, and it fights EF Core's model caching.
- **Filtering by hand in every query**: one forgotten `Where` is a data leak. Rejected in favour of filters that apply by default.

## Consequences
- New code is isolated by default. Opting out (`IgnoreQueryFilters`) is greppable and reviewed.
- Raw SQL bypasses the EF filters, so every raw query must filter by tenant explicitly until row-level security lands.
- Background jobs (outbox dispatcher) have no user token. They set the tenant context from the message's `TenantId` explicitly.
