# Mini ERP: specification

**Status:** draft v0.2 · **Owner:** Stefan · **Last updated:** 2026-11-14
**Name:** `mini-erp` (code namespace `MiniErp`)

---

## 1. Purpose

A multi-tenant backend for small businesses that covers **catalog → inventory → sales orders → shipments → invoices → e-invoice submission**. It is modelled on how a real ERP (Microsoft Dynamics 365 Business Central) posts documents, rebuilt from scratch on .NET.

This is a **portfolio and learning project**, not a product. Every scope decision optimises for showing backend engineering skill at medior level: correctness under concurrency, transactional integrity, clean module boundaries, resilience to an unreliable external system, and real tests against a real database.

### Goals
| # | Goal | How it is shown |
|---|---|---|
| G1 | Correct business logic in a non-trivial domain | Posting rules, append-only ledger, invariants enforced and tested |
| G2 | Correctness under concurrency | Two orders racing for the last unit: exactly one wins, proven by a parallel integration test |
| G3 | Transactional integrity | Posting is all-or-nothing; events leave through a transactional outbox |
| G4 | Resilience | Integration with a deliberately flaky e-invoice simulator: retries, timeouts, circuit breaker, idempotency |
| G5 | Security and isolation | OIDC auth, role-based permissions, strict tenant isolation, with tests that try to cross tenants |
| G6 | Operability | One-command local start, structured logs, traces, metrics, health checks |
| G7 | Professional engineering | CI, integration tests on real PostgreSQL, ADRs for every significant decision, a clear README |

### Non-goals
- A frontend. The API is the product until phase 4 (see §8).
- Accounting: general ledger, chart of accounts, VAT returns. Invoices stop at "posted and submitted".
- Purchasing. Stock comes in through a simple receipt or adjustment journal.
- Multiple currencies (everything is in RSD), units-of-measure conversion, lot or serial tracking, pricing rules beyond a unit price per item.
- Production hosting. The system must run locally; a hosted demo is optional.

### Hard constraint: free only
Everything used must be free: open-source tools and libraries, or free tiers that need no payment. No paid cloud services and no commercially licensed libraries. See [ADR-0007](adr/0007-free-dependencies-only.md). Cloud-specific choices stay behind configuration and adapters, so that a paid managed service could replace a container with a configuration change rather than a rewrite.

---

## 2. Users and roles

Users belong to exactly one **tenant** (a company). Roles are per user:

| Role | Can do |
|---|---|
| `Admin` | Everything in the tenant, including number series and user roles |
| `Sales` | Customers and sales orders: create, release, reopen, cancel |
| `Warehouse` | Warehouses, stock receipts and adjustments, posting shipments |
| `Finance` | Posting invoices and credit memos, e-invoice submission and status |

Everyone in a tenant can read the catalog and stock levels.

---

## 3. Modules

The system is a **modular monolith** ([ADR-0001](adr/0001-modular-monolith.md)): one deployable, with modules that own their tables and talk to each other only through public contracts.

```
┌─────────────┐   ┌──────────────┐   ┌─────────────┐   ┌──────────────┐
│   Catalog   │   │  Inventory   │   │    Sales    │   │  Invoicing   │
│ items       │◄──│ warehouses   │◄──│ customers   │──►│ invoices     │
│ (VAT, price)│   │ ledger       │   │ orders      │   │ credit memos │
│             │   │ reservations │   │ shipments   │   │ e-invoicing  │
└─────────────┘   └──────────────┘   └─────────────┘   └──────┬───────┘
                                                              │ outbox
        Platform: tenancy · auth · number series · outbox     ▼
                  · idempotency · auditing            ┌──────────────────┐
                                                      │ e-invoice        │
                                                      │ simulator (HTTP) │
                                                      └──────────────────┘
```

| Module | Owns | BC equivalent |
|---|---|---|
| **Catalog** | Items: SKU, name, unit price, VAT rate, blocked flag | Item |
| **Inventory** | Warehouses, inventory ledger entries, balances, reservations | Location, Item Ledger Entry, Reservation Entry |
| **Sales** | Customers, sales orders and lines, posted shipments | Customer, Sales Header/Line, Posted Sales Shipment |
| **Invoicing** | Posted invoices, credit memos, e-invoice submissions | Posted Sales Invoice, Posted Sales Cr. Memo |
| **Platform** | Tenants, number series, outbox, idempotency keys, audit fields | Company, No. Series |

---

## 4. Domain model

### 4.1 Catalog
- **Item**: `Id`, `TenantId`, `Sku` (unique per tenant), `Name`, `UnitPrice` (decimal, RSD), `VatRate` (`Standard` 20%, `Reduced` 10%, `Exempt` 0%), `IsBlocked`, `CreatedAt`, `UpdatedAt`.

### 4.2 Inventory
- **Warehouse**: `Id`, `TenantId`, `Code` (unique per tenant), `Name`.
- **InventoryLedgerEntry** (append-only, [ADR-0004](adr/0004-append-only-inventory-ledger.md)): `Id`, `TenantId`, `ItemId`, `WarehouseId`, `EntryType` (`Receipt`, `PositiveAdjustment`, `NegativeAdjustment`, `Shipment`, `ShipmentReversal`), `Quantity` (signed), `DocumentType`, `DocumentNo`, `PostedAt`, `PostedBy`.
- **InventoryBalance** (projection, updated in the same transaction as the ledger): `TenantId`, `ItemId`, `WarehouseId`, `OnHand`, `Reserved`, `RowVersion`. `Available = OnHand − Reserved`.
- **Reservation**: `Id`, `TenantId`, `ItemId`, `WarehouseId`, `SalesOrderLineId`, `Quantity`, `CreatedAt`.

### 4.3 Sales
- **Customer**: `Id`, `TenantId`, `No`, `Name`, `TaxId` (PIB, 9 digits, checksum validated), `RegistrationNo` (matični broj, 8 digits), `Address`, `Email`, `IsBlocked`.
- **SalesOrder**: `Id`, `TenantId`, `No`, `CustomerId`, customer snapshot (name, tax id, address), `WarehouseId`, `Status` (`Draft`, `Released`, `PartiallyShipped`, `Shipped`, `Invoiced`, `Cancelled`), `OrderDate`, `RowVersion`.
- **SalesOrderLine**: `Id`, `LineNo`, `ItemId`, item snapshot (SKU, name), `Quantity`, `UnitPrice`, `VatRate` (snapshots taken when the line is added), `QuantityShipped`, `QuantityInvoiced`, `QuantityClosed`. Outstanding quantity = `Quantity − QuantityShipped − QuantityClosed`.
- **PostedShipment** + lines: immutable record of what was shipped, with `No`, `SalesOrderId`, `PostedAt` and the quantity per line.

### 4.4 Invoicing
- **PostedInvoice** + lines: immutable. `No` (gapless, [ADR-0009](adr/0009-document-numbering.md)), `SalesOrderId`, customer snapshot, `PostingDate`, line amounts, `NetAmount`, `VatAmount`, `GrossAmount`, `EInvoiceStatus`.
- **PostedCreditMemo** + lines: immutable, references the invoice it corrects. *(Phase 3, stretch.)*
- **EInvoiceSubmission**: `InvoiceId`, `ExternalId`, `Status` (`Pending`, `Sent`, `Accepted`, `Rejected`, `Failed`), `Attempts`, `LastError`, timestamps.

### 4.5 Platform
- **Tenant**: `Id`, `Name`, `TaxId`.
- **NumberSeries**: `TenantId`, `Code` (`SO`, `SHIP`, `INV`, `CM`, `CUST`), `Prefix`, `LastNo`, `Year` (where it resets yearly), `IsGapless`.
- **OutboxMessage**: `Id`, `TenantId`, `Type`, `Payload` (jsonb), `OccurredAt`, `ProcessedAt`, `Attempts`, `LastError` ([ADR-0005](adr/0005-transactional-outbox.md)).
- **IdempotencyRecord**: `TenantId`, `Key`, `RequestHash`, `ResponseStatus`, `ResponseBody`, `CreatedAt`.

---

## 5. Sales order lifecycle

```
              release                post shipment               post shipment
   Draft ─────────────────► Released ─────────────► PartiallyShipped ─────────► Shipped
     ▲   (reserves stock)      │                    (some quantity left)    (all shipped)
     └──────── reopen ─────────┘           close remainder ──────────────────────┤
         (removes reservations;                                                  │ post invoice
          only if nothing shipped)                                               ▼
                                                                             Invoiced
   Draft or Released ── cancel (removes reservations) ──► Cancelled         (all invoiced)
```

Invoices can be posted from `PartiallyShipped` and `Shipped` for whatever has been shipped but not yet invoiced. The order becomes `Invoiced` when no line has an outstanding quantity and every shipped quantity is invoiced.

- **Draft**: lines can be added, changed and removed. Nothing is reserved.
- **Release**: every line reserves its full quantity in the order's warehouse, all or nothing. If any line lacks available stock, the release fails, nothing is reserved, and the response says which lines were short.
- **Reopen**: back to Draft. Reservations for unshipped quantities are removed. Not allowed once anything has been shipped.
- **Post shipment**: ships some or all of the reserved quantity per line. It writes a negative `Shipment` ledger entry, lowers `OnHand` and `Reserved`, raises `QuantityShipped`, and creates a `PostedShipment`. Whatever is not shipped stays reserved as a back order.
- **Close remainder**: from `PartiallyShipped`, ends the back order. Every line's outstanding quantity moves to `QuantityClosed`, its reservation is removed, and the order becomes `Shipped` (BR-16).
- **Post invoice**: invoices shipped but not yet invoiced quantities. It creates a `PostedInvoice` with a gapless number and enqueues the e-invoice submission through the outbox.
- **Cancel**: only from Draft or Released with nothing shipped. Releases all reservations.

---

## 6. Business rules

Each rule has an id. Tests reference these ids in their names, so any rule can be traced to the tests that prove it.

| Id | Rule |
|---|---|
| **BR-01** | Ledger entries are never updated or deleted. Corrections are new entries. |
| **BR-02** | `Available = OnHand − Reserved`. A reservation never makes `Available` negative, under any concurrency. |
| **BR-03** | `OnHand` never goes negative. A negative adjustment larger than the available quantity is refused. |
| **BR-04** | Shipped quantity per line ≤ reserved quantity for that line. |
| **BR-05** | Invoiced quantity per line ≤ shipped quantity for that line. |
| **BR-06** | Posted shipments, invoices and credit memos are immutable. Mistakes are corrected with a credit memo or a shipment reversal. |
| **BR-07** | Released orders cannot have their lines edited. Reopen first. |
| **BR-08** | Price, VAT rate, item name and customer data are copied onto the document when it is created. Later catalog changes do not alter existing documents. |
| **BR-09** | Invoice and credit memo numbers are gapless and sequential per tenant per year (e.g. `INV-2026-00001`). Order, shipment and customer numbers are unique but may have gaps. |
| **BR-10** | Money is `decimal` with 2 decimals, rounding `MidpointRounding.AwayFromZero`. VAT is calculated and rounded per line. Document totals are sums of rounded line amounts. |
| **BR-11** | A blocked item cannot be added to an order. A blocked customer cannot get new orders or releases. |
| **BR-12** | An item or warehouse with ledger entries cannot be deleted, only blocked. |
| **BR-13** | Tenant isolation: no request can read or change another tenant's data. Another tenant's id answers `404`, never `403`, so ids from other tenants cannot be probed. |
| **BR-14** | Every state-changing `POST` accepts an `Idempotency-Key` header. Repeating the request with the same key and body returns the original response without repeating the effect. The same key with a different body is `422`. |
| **BR-15** | Customer `TaxId` (PIB) must pass the official checksum. `RegistrationNo` must be 8 digits. |
| **BR-16** | After a partial shipment the outstanding quantity stays reserved (back order) until it is shipped or explicitly closed. Closing releases the reservation and can't be undone; a new order is needed for the rest. Each order has exactly one warehouse, set on the header. |

---

## 7. API

- REST over HTTPS, JSON, ASP.NET Core minimal APIs grouped per module: `/api/catalog/...`, `/api/inventory/...`, `/api/sales/...`, `/api/invoicing/...`.
- **Commands as actions** where the action is a business operation, not a field update: `POST /api/sales/orders/{id}/release`, `/reopen`, `/cancel`, `/close-remainder`, `/shipments`, `/invoices`.
- **Errors** are RFC 9457 Problem Details, with a stable `code` extension (e.g. `inventory.insufficient_stock`) and the business rule id where one applies.
- **Optimistic concurrency** on documents: responses carry an `ETag`; updates require `If-Match` and answer `412` on a mismatch.
- **Pagination**: cursor-based (`?after=...&limit=...`) on lists that grow (ledger, documents); offset-based elsewhere.
- **OpenAPI** document generated by ASP.NET Core and browsable with Scalar. `.http` files in the repo cover the main flows.
- **Versioning**: URL prefix `/api/v1` once a frontend consumes the API (phase 4). Until then there is no versioning, deliberately.

---

## 8. Phases

Each phase ends with something that runs, is tested and can be shown. A phase is done only when its acceptance criteria pass in CI.

### Phase 1: Foundation
- Solution structure, modules, architecture tests that enforce module boundaries.
- `docker compose up` starts the API, PostgreSQL, Keycloak (with an imported realm, two tenants and seeded users), and the observability dashboard.
- Auth: JWT validation, `tenant_id` and role claims, permission policies.
- Tenancy: tenant resolved from the token, EF Core global query filters, `TenantId` on every row.
- Catalog: item CRUD. Sales: customer CRUD with PIB validation.
- Number series, Problem Details, OpenAPI + Scalar, health checks.
- CI: build, unit tests, integration tests on PostgreSQL via Testcontainers.

**Acceptance:** a user of tenant A cannot see or change any item or customer of tenant B (tested on every endpoint). CI is green. A fresh clone runs with `docker compose up` and one documented command.

### Phase 2: Inventory and sales orders
- Warehouses, receipt and adjustment journal, append-only ledger, balance projection.
- Sales orders: draft, lines, release with reservation, reopen, cancel, post shipment (partial, with back order), close remainder.
- Optimistic concurrency on balances ([ADR-0008](adr/0008-stock-reservation-concurrency.md)); ETag/If-Match on orders.
- Idempotency keys on all state-changing `POST`s.

**Acceptance:** a test fires N parallel releases for more units than exist; exactly the stock-covered ones succeed, and `Available` is never negative (BR-02). Ledger and balance always reconcile: a test sums the ledger and compares it with the balances.

### Phase 3: Invoicing and e-invoicing
- Post invoice (partial allowed), gapless numbering, invoice PDF.
- Transactional outbox and a background dispatcher.
- E-invoice simulator: a separate small service with configurable latency, failure rate and asynchronous accept/reject.
- Resilient HTTP client: timeout, retry with jitter, circuit breaker; status polling; idempotent submission.
- *Stretch:* credit memos, shipment reversal.

**Acceptance:** with the simulator failing 30% of requests, every posted invoice ends `Accepted` or `Rejected` with no duplicate submissions. Invoice numbers have no gaps under parallel posting (tested).

### Phase 4: Production readiness and a client
- Traces across HTTP → database → outbox → simulator; metrics such as orders released and invoice submission latency.
- Rate limiting, audit trail (who changed what), PostgreSQL row-level security as a second tenant guard (stretch).
- Load test with k6: published numbers in the README.
- A small React + TypeScript admin UI with an API client generated from OpenAPI.
- Optional hosted demo on a free tier.

### Later, optional
- A Business Central (AL) extension that sends customers or orders into this system: a bridge to BC roles.
- Reporting read models (stock valuation, sales per customer) with plain SQL.

---

## 9. Non-functional requirements

| Area | Requirement |
|---|---|
| **Testing** | Business rules are covered by unit tests on the domain. Every endpoint has integration tests against real PostgreSQL (Testcontainers). Concurrency rules (BR-02, BR-09) have parallel tests. No mocks of the database. |
| **Isolation** | Every tenant-scoped endpoint has a cross-tenant test (BR-13). |
| **Observability** | Structured logs carry the trace id, tenant id and user id. OpenTelemetry traces and metrics. `/health/live` and `/health/ready`. |
| **Security** | No secrets in the repo: local secrets come from `.env` (git-ignored) with a committed `.env.example`. Dependencies are scanned in CI. |
| **Local start** | A fresh clone runs with Docker and the .NET SDK only. Seed data gives a working demo within a minute. |
| **Documentation** | README (what, why, how to run, architecture diagram); ADRs for every significant decision; this specification kept current. |

---

## 10. Technology (initial choices)

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | Supported until November 2028 |
| API | ASP.NET Core minimal APIs, built-in OpenAPI, Scalar UI | |
| Data | PostgreSQL 18, EF Core + Npgsql | [ADR-0002](adr/0002-postgresql-and-ef-core.md) |
| Auth | Keycloak (OIDC) in Docker, JWT bearer in the API | [ADR-0006](adr/0006-keycloak-for-identity.md) |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly) | |
| Background work | `BackgroundService` + PostgreSQL outbox | [ADR-0005](adr/0005-transactional-outbox.md) |
| Observability | OpenTelemetry → .NET Aspire dashboard (standalone container) | |
| Tests | xUnit, Testcontainers, `WebApplicationFactory`, Shouldly, NSubstitute | No FluentAssertions v8+ (commercial) |
| PDF | to be decided in phase 3 | Must fit [ADR-0007](adr/0007-free-dependencies-only.md) |
| Load test | k6 | |
| CI | GitHub Actions | |

Every library is checked against [ADR-0007](adr/0007-free-dependencies-only.md) before it is added.

---

## 11. Resolved questions

| # | Question | Decision (2026-11-14) |
|---|---|---|
| 1 | Project name | `mini-erp`, namespace `MiniErp`. A descriptive name tells a recruiter what the project is at a glance. |
| 2 | Auth in phase 1 | Keycloak from day one, with an imported realm ([ADR-0006](adr/0006-keycloak-for-identity.md)). Auth is real from the first endpoint, and nothing needs reworking later. |
| 3 | Warehouses per order | One warehouse per order, on the header, like BC's default Location Code. Multi-warehouse orders are out of scope. |
| 4 | Partial shipments | Back order is the default: the unshipped remainder stays reserved for a later shipment. The remainder can be closed explicitly (§5, BR-16). |

## 12. Open questions

None at the moment.
