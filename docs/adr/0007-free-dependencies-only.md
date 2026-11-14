# ADR-0007: Free dependencies only

**Status:** Accepted · **Date:** 2026-11-14

## Context
This is a learning and portfolio project with no budget. In 2025 several popular .NET libraries moved to commercial licences, among them MediatR, AutoMapper, MassTransit (v9) and FluentAssertions (v8). A dependency picked casually today can become a licence problem with the next major version.

## Decision
- Only dependencies that are free for this use: OSI-approved open-source licences (MIT, Apache 2.0, BSD, PostgreSQL), or free tiers that need no payment and no card.
- **Not used:** MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+, NServiceBus, paid cloud services.
- **Instead:** plain handler classes called directly or through DI (no mediator); hand-written mapping (explicit, and it makes mistakes visible at compile time); the PostgreSQL outbox ([ADR-0005](0005-transactional-outbox.md)); Shouldly or plain xUnit assertions.
- Major versions are pinned. Before a dependency is added or a major version is bumped, its licence is checked and noted in the pull request.
- Infrastructure runs as containers locally. A cloud-specific service may be added only behind an interface with a local implementation.

## Alternatives considered
- **"Free for individuals" licences** (community editions with revenue limits): allowed only when no OSI-licensed option is reasonable, and recorded in an ADR, since the terms can change.

## Consequences

**What gets easier**
- Anyone can clone and run the project with no costs, accounts or licences. The versions in use cannot become paid later: a licence change only applies to new versions, and major versions are pinned. A free tier of a hosted service, by contrast, can change or disappear at any time.
- Mapping and handler code is explicit: the compiler catches a renamed or added field, and an interviewer can read the code without knowing a library.

**What gets harder**
- Some convenience code (mapping, dispatch) is written by hand.
- Every new dependency costs a licence check, and some well-known libraries are off the table.

**Worth knowing**
- Free does not mean supported forever: .NET 10 gets security fixes until November 2028, and container images can be retired from registries. Upgrades remain a normal maintenance task.
- This record is a talking point: it shows awareness of licensing risk in a real dependency tree.
