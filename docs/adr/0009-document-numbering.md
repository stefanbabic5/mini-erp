# ADR-0009: Gapless invoice numbers, gaps allowed elsewhere

**Status:** Accepted · **Date:** 2026-11-14

## Context
Invoices need sequential numbers without gaps per tenant per year (BR-09). A missing number has to be explained to a tax auditor. Other documents (orders, shipments, customers) only need unique numbers. A gapless series is a serialisation point: two invoices cannot take numbers in parallel without coordination. Business Central's No. Series has the same trade-off.

## Decision
- **Gapless series** (`INV`, `CM`): the next number is taken by locking the series row (`SELECT ... FOR UPDATE`) **inside the posting transaction**, incrementing `LastNo`, and posting. If posting fails, the transaction rolls back and the number is never used, so there are no gaps. Concurrent postings in the same tenant wait for each other on that row.
- **Series with gaps allowed** (`SO`, `SHIP`, `CUST`): the number comes from a PostgreSQL sequence per tenant and series, or the same counter row updated in its own short transaction. No waiting, but a failed request leaves a gap.
- The lock is taken as late as possible in the posting transaction, after validation, to keep the time it is held short.
- **Proof:** a parallel integration test posts N invoices at once and asserts the numbers are exactly 1..N with no gaps and no duplicates.

## Alternatives considered
- **A sequence for invoices too**: fast, but sequences are not transactional. A rolled-back posting burns a number and creates a gap.
- **`MAX(No) + 1` at posting time**: two concurrent transactions read the same maximum. It needs a lock anyway, and taking it on the whole table is worse.
- **Assigning invoice numbers in a later batch job**: gapless and parallel, but documents have no number at posting time, which users and the e-invoice submission need.

## Consequences
- Invoice posting within one tenant is serialised. That is acceptable for small businesses, and the load test shows its limit.
- Different tenants never block each other (one series row per tenant).
- Number formatting (`INV-2026-00001`) and yearly reset are part of the series definition.
