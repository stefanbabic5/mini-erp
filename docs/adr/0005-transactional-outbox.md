# ADR-0005: Transactional outbox in PostgreSQL

**Status:** Accepted · **Date:** 2026-11-14

## Context
Posting an invoice must also submit it to the external e-invoice service. Calling the service inside the posting transaction fails both ways. If the call succeeds and the commit fails, the service has an invoice we don't. If the commit succeeds and the call fails, we have an invoice the service never got. The service is deliberately unreliable ([specification §8, phase 3](../specification.md)).

## Decision
- The posting transaction also inserts an `OutboxMessage` (`InvoicePosted`, with a `jsonb` payload) into `platform.outbox_messages`. Commit means both happened; rollback means neither did.
- A `BackgroundService` dispatcher polls for unprocessed messages with `SELECT ... FOR UPDATE SKIP LOCKED`, so several instances never take the same message. It calls the handler and then marks the message processed.
- Delivery is **at least once**. Handlers must be idempotent: the e-invoice submission sends the invoice id as the idempotency key to the external service, and a handler that finds the submission already sent does nothing.
- Failed messages are retried with exponential backoff and jitter, up to a limit, and then marked failed for inspection, which acts as a dead-letter state.

## Alternatives considered
- **Call the service directly after commit**: a crash between the commit and the call loses the submission silently.
- **A message broker (RabbitMQ, Kafka)**: it still needs an outbox to be atomic with the database write, and it adds an infrastructure component. The PostgreSQL outbox can later feed a broker without changing the posting code.
- **A messaging library (MassTransit, NServiceBus)**: MassTransit v9 and NServiceBus are commercially licensed ([ADR-0007](0007-free-dependencies-only.md)). Wolverine (MIT) remains an option if the hand-written outbox grows too big. Writing it by hand is also the point of the exercise.

## Consequences
- Posting never depends on the external service being up.
- Submission happens after a short delay (the polling interval), so the API returns `EInvoiceStatus = Pending`.
- The outbox table needs indexing on unprocessed rows and a retention job for processed ones.
