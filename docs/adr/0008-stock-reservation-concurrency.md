# ADR-0008: Optimistic concurrency for stock reservation

**Status:** Accepted · **Date:** 2026-11-14

## Context
Releasing an order reserves stock (BR-02: `Available` never negative). Two users releasing orders for the last unit at the same moment must not both succeed. A naive read, check, then write lets both requests read `Available = 1`, both pass the check, and both reserve.

## Decision
- **Optimistic concurrency on `InventoryBalance`**, using PostgreSQL `xmin` as the concurrency token ([ADR-0002](0002-postgresql-and-ef-core.md)). The release reads the balances, checks availability, adds the reservations, and saves. If another transaction changed a balance in between, EF Core throws `DbUpdateConcurrencyException`.
- On that conflict, the whole release is retried from a fresh read, a few times with a short jittered delay. If stock really is gone, the retry fails the availability check and returns `409` with `inventory.insufficient_stock`.
- An order's balance rows are always touched in a fixed order (by item id), so concurrent multi-line releases cannot deadlock.
- **Proof:** an integration test on real PostgreSQL fires N parallel releases against stock for fewer than N and asserts that exactly the right number succeed and the final balances reconcile with the ledger.

## Alternatives considered
- **Pessimistic locking (`SELECT ... FOR UPDATE`)**: also correct, and better when conflicts are frequent. Rejected as the default because conflicts are rare in this workload and locks are held for the whole transaction. It remains the fallback if the load test shows retry storms on hot items, and it is used where gaps are not allowed ([ADR-0009](0009-document-numbering.md)).
- **An atomic conditional update** (`UPDATE ... SET reserved = reserved + @q WHERE available >= @q`): the fastest option, but it moves the business rule into SQL, and multi-line all-or-nothing releases get awkward. A good later optimisation for single-line paths.
- **A serializable transaction isolation level**: correct, but it turns every conflict into a serialization failure across all tables touched, and it is harder to reason about.

## Consequences
- No locks are held while the business logic runs. Throughput is good when conflicts are rare.
- Retry logic must cover the whole unit of work, not a single statement. This is a single helper used by every command that writes balances.
- Under heavy contention for one item, retries add latency. The phase 4 load test measures it.
