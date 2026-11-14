# ADR-0002: PostgreSQL with EF Core

**Status:** Accepted · **Date:** 2026-11-14

## Context
The domain is relational: documents with lines, ledgers, balances and strict invariants. It needs ACID transactions, row-level locking for gapless numbering, and real concurrency control. Everything must be free ([ADR-0007](0007-free-dependencies-only.md)) and run in Docker.

## Decision
- **PostgreSQL 18** as the only data store, including the outbox and idempotency records.
- **EF Core with Npgsql** for writes and most reads, with migrations per module `DbContext`.
- PostgreSQL's system column `xmin` is the concurrency token, so no manual row-version columns are needed.
- Plain SQL (EF Core raw SQL or Dapper) is allowed for reporting reads and for the few statements EF Core expresses poorly (`SELECT ... FOR UPDATE SKIP LOCKED` in the outbox dispatcher).
- `jsonb` for outbox payloads and stored idempotent responses.

## Alternatives considered
- **SQL Server (Developer or Express edition)**: free, and closer to the Microsoft stack. Rejected: the Developer edition's licence forbids production use and Express has database size limits, while PostgreSQL is fully free with no editions. EF Core keeps a later switch feasible.
- **A document database (MongoDB) or Firestore**: weaker multi-document transactions, and the portfolio already has a NoSQL project (drugar-app).
- **Dapper only**: more control, but a lot of mapping code, and no migrations.

## Consequences
- Integration tests run against the same engine as production through Testcontainers, so no in-memory provider hides SQL differences.
- `xmin` is PostgreSQL-specific. A database switch would replace it with a `rowversion` column, which is contained in each entity's EF configuration.
