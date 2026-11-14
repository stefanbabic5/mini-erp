# ADR-0004: Append-only inventory ledger with a balance projection

**Status:** Accepted · **Date:** 2026-11-14

## Context
Stock must be auditable: for any item and warehouse, it must be possible to explain the current quantity from its history. Business Central does this with Item Ledger Entries, which are never changed after posting. Stock levels are also read constantly (order release, item views), so summing the whole history on every read would get slower as the history grows.

## Decision
- **`InventoryLedgerEntry` is append-only** (BR-01): rows are inserted, never updated or deleted. A correction is a new entry with the opposite sign (e.g. `ShipmentReversal`).
- **`InventoryBalance` is a projection** per `(TenantId, ItemId, WarehouseId)` holding `OnHand` and `Reserved`. It is updated **in the same transaction** as the ledger insert, so the two can never disagree.
- The database enforces append-only: the application's database role has no `UPDATE` or `DELETE` permission on the ledger table. Migrations run under a separate owner role.
- A reconciliation check (a test, and later an admin endpoint) compares `SUM(Quantity)` from the ledger with `OnHand` for every balance row.

## Alternatives considered
- **Only a mutable quantity column on the item**: simple, but no history, no audit trail, and nothing to reconcile against.
- **Only the ledger, summed on every read**: always correct, but reads get slower as the ledger grows. BC solves this with SIFT indexes; a projection table is the equivalent here.
- **Event sourcing for the whole domain**: the ledger is already event-sourced in spirit. Applying it to every aggregate adds complexity (snapshots, versioned events, rebuilt read models) without a matching benefit in this project.

## Consequences
- History is complete and trustworthy; the reconciliation test proves the projection.
- Every posting writes two tables in one transaction, and the balance row is the point of contention under concurrency ([ADR-0008](0008-stock-reservation-concurrency.md)).
- Fixing a wrong posting always takes a new document, which matches how accountants and ERP users expect it to work.
