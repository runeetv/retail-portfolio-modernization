# Implementation Progress

Environment: Windows, .NET 10.0.402 SDK, SQL Server LocalDB (MSSQLLocalDB, v17).
Connection: `(localdb)\MSSQLLocalDB` / database `LegacyRetailDB`.
Override via env var `LEGACYRETAIL_CONNECTION`.

## Phase 1 — Repo, schema, provisioning, seed  ✅ DONE

- Created repository structure under `src/`, `database/`, `tests/`, `benchmark/`, `docs/`, `scripts/`.
- Solution: `LegacyRetailPortfolio.slnx` (.NET 10 creates `.slnx` by default).
- Shared infra-only project `LegacyRetail.Common` (connection config, PBKDF2 hashing). No business logic or entities.
- SQL schema split by logical owner:
  - `database/schema/01_schema.sql` — shared (Customers, Products, ProductCategories, Orders, OrderLines, Inventory, Returns, ReturnLines).
  - `database/schema/02_orderdesk.sql` — OrderDesk tables.
  - `database/schema/03_customerportal.sql` — CustomerPortal tables.
  - `database/schema/04_fulfillment.sql` — FulfillmentAPI tables.
- Stored procedures `database/procedures/procedures.sql`:
  - `usp_ConvertQuoteToOrder` (OrderDesk; transactional, prevents double-convert, computes commission).
  - `usp_EvaluateCancellationEligibility` (OrderDesk cancellation rule in SQL).
  - `usp_AllocateInventory` (FulfillmentAPI; blocks over-allocation and cancelled orders).
  - `usp_ReceiveReturnAndAdjust` (FulfillmentAPI; idempotent receipt + restock, no refund).
  - `usp_WarehouseActivitySummary` (complex cross-domain join across 3 domains).
- `DbProvisioner` console app: creates DB, applies scripts, seeds deterministic data.
  - Flags: `--reset`, `--schema-only`, `--seed-only`. Idempotent (skips seed if data present).

### Verified seed counts (meets/exceeds requirements)
Customers 120, CustomerAccounts 80, Products 120, ProductCategories 8, Orders 240,
OrderLines 584, SalesQuotes 40, DiscountApprovals 23, SalesCommissions 40,
ShoppingCarts 58, Coupons 4, CouponRedemptions 15, Warehouses 3, WarehouseBins 36,
InventoryAllocations 304, PickLists 126, Shipments 81, CarrierEvents 184,
Returns 22, ReturnReceipts 8, SupportTickets 35, SalesAgentTasks 30.

### Verified stored procedures
- Quote→order conversion produced Order 241 + commission.
- Cancellation eligibility returns eligible for Confirmed order.
- Warehouse summary returns cross-domain aggregates per warehouse.

## Deviations from spec
- Solution file is `.slnx` (XML) rather than `.sln` — this is the .NET 10 SDK default. Functionally equivalent.
- A dev-only patched `System.Security.Cryptography.Pkcs` is pinned centrally to clear a transitive advisory from `Microsoft.Data.SqlClient`.

## Phase 2 — OrderDesk  (in progress)

### Phase 2 — OrderDesk  ✅ DONE

Projects: `OrderDesk.Domain`, `OrderDesk.Application`, `OrderDesk.Infrastructure`, `OrderDesk.Desktop` (WPF, net10.0-windows).

- Own entity model (distinct from other apps even for shared tables): Customer, Product, Order, etc.
- Own `OrderDeskDbContext` mapping onto shared schema (no runtime migrations).
- Pure rules in `OrderDeskRules` (discount threshold 15%, quote validity 14d, staff cancellation = Pending/Confirmed/Allocated, commission formula, mandatory-info guard).
- Application services:
  - `CustomerService` — staff customer search + full contact edit (Scenario 1).
  - `CatalogService` — product browse + availability check (Scenario 4 OrderDesk variant).
  - `QuoteService` — create quote, add lines, apply discount (auto-approve or route to DiscountApproval), decide approval, convert to order via `usp_ConvertQuoteToOrder`.
  - `OrderService` — staff order creation (Scenario 2), cancellation via `usp_EvaluateCancellationEligibility` with authorized override + audit (Scenario 3), read-only shipment status (Scenario 6).
  - `ReturnReviewService` — approve/reject customer returns (Scenario 5 / Workflow C).
  - `AgentTaskService` — agent tasks + commissions.
- WPF MVVM desktop with tabs: Customers, Products, Quotations, Orders, Discount Approvals, Returns, Agent Tasks. Custom ObservableObject + Relay/AsyncRelayCommand.

Verified: full solution builds clean (0 warnings/errors). Smoke test against live DB ran the entire quote→order workflow successfully (created order 242 via stored proc, commission recorded, eligibility evaluated).

Package notes: Microsoft.Data.SqlClient pinned 6.1.7 (matches EF Core 10.0.14 requirement); patched System.Security.Cryptography.Pkcs 9.0.22 pinned in data-layer projects only (WPF project auto-prunes it). `App : System.Windows.Application` fully-qualified to avoid clash with `OrderDesk.Application` namespace.

### Phase 3 — CustomerPortal  ✅ DONE

Projects: `CustomerPortal.Domain`, `CustomerPortal.Application`, `CustomerPortal.Infrastructure`, `CustomerPortal.Web` (ASP.NET Core MVC + Razor, net10.0).

- Own domain model: `CustomerAccount` (login) distinct from OrderDesk's `Customer`; `ShopperProfile` is a SECOND C# model over dbo.Customers (narrower editable subset). Third distinct order model `CustomerOrder`.
- Own `PortalDbContext` over the shared schema.
- Rules in `PortalRules` implemented in C# (NOT SQL): customer cancellation = Pending/Confirmed only (STRICTER than OrderDesk's SQL rule which allows Allocated — the bounded divergence); 30-day return window; coupon eligibility (type/status/min-total/redemption cap); ticket status transitions.
- Services: Auth (PBKDF2), Profile (self-service subset), CatalogBrowse (availability display), Cart (+coupon), Checkout (idempotent + inventory recheck), OrderHistory (+ cancellation, ownership), Return (create requests), Tracking (read FulfillmentAPI data), SupportTicket (valid transitions), Wishlist.
- MVC: cookie auth, antiforgery on all POST forms, `[Authorize]` controllers, ownership filtering by signed-in customer. Bootstrap 5 UI. Controllers: Account, Home, Catalog, Cart, Checkout, Orders, Returns, Wishlist, Support, Profile.

Verified via live HTTP smoke test against running app + DB:
- Home/Catalog 200; anonymous protected page -> 302 login redirect; login -> 302 (cookie); add-to-cart -> 302.
- Checkout idempotency CONFIRMED: two POSTs with the same key both returned Order 243 (no duplicate).
- Authenticated order list 200.

Fix applied (also to OrderDesk): user-initiated transactions must run inside `Database.CreateExecutionStrategy().ExecuteAsync(...)` because `EnableRetryOnFailure` is on. Both CheckoutService and OrderDesk.OrderService.CreateStaffOrderAsync now wrap their transaction in the execution strategy.

Dev credentials: usernames `user{CustomerId}` (e.g. user1..user80), password `Password123!`. URLs: http://localhost:5101 / https://localhost:7101.

### Phase 4 — FulfillmentAPI  ✅ DONE

Projects: `FulfillmentAPI.Domain`, `FulfillmentAPI.Application`, `FulfillmentAPI.Infrastructure`, `FulfillmentAPI.Api` (ASP.NET Core Web API, controllers, net10.0).

- Own warehouse-centric model (`FulfillmentOrder` = same dbo.Orders row the others call Order/CustomerOrder - naming inconsistency on purpose). Own `FulfillmentDbContext`.
- Rules in `FulfillmentRules`: cancelled orders block fulfillment; picking needs prior allocation; packaging needs all items picked; dispatch needs package + carrier reference; shipment transition validation.
- Services: FulfillmentQuery (pending orders, availability, tracking, activity summary SP), Allocation (usp_AllocateInventory), Picking (sequencing enforced), Shipment (packaging/status/carrier events idempotent), ReturnReceipt (usp_ReceiveReturnAndAdjust).
- API: controller-based, Swagger/OpenAPI with documented API key scheme, `X-Api-Key` middleware auth (dev key `dev-fulfillment-key`), centralized exception handling (RFC7807 JSON), structured logging, standard HTTP status codes, model validation. Health endpoint. `.http` test collection.
- Stored-proc-based operations: usp_AllocateInventory + usp_ReceiveReturnAndAdjust (meets "at least two" requirement), plus usp_WarehouseActivitySummary for reporting.

Verified via live HTTP smoke test against running API + DB:
- Auth: no key -> 401, with key -> 200, /health exempt -> 200. Swagger JSON + UI -> 200.
- Full Workflow E on order 5: allocate (1 product) -> pick list 127 -> pick item 305 -> shipment 82 (Packed) -> dispatch.
- Rule guards CONFIRMED: dispatch without carrier -> 409; dispatch with carrier -> 200; carrier event idempotency -> both calls returned id=185 (no duplicate).
- Cross-domain activity summary SP -> 200.

Swashbuckle 10.3.1 note: OpenAPI.NET 2.0 moved types to `Microsoft.OpenApi` namespace; security requirement uses `OpenApiSecuritySchemeReference` and `AddSecurityRequirement(doc => ...)` lambda overload.

URLs: http://localhost:5201 / https://localhost:7201 (Swagger at /swagger).

**Full solution builds clean (0 errors, 0 warnings).**

### Phase 5 — Tests  ✅ DONE

Four xUnit test projects (xunit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0). **70 tests pass, 0 failures.**

Unit tests (pure domain rules, no DB):
- `OrderDesk.Tests` (15): staff cancellation rule, discount threshold, convert eligibility, commission formula, mandatory-info guard.
- `CustomerPortal.Tests` (16): customer cancellation (+ explicit assertion of the bounded divergence vs OrderDesk on 'Allocated'), return window, coupon eligibility/discount, ticket transitions.
- `FulfillmentAPI.Tests` (18): fulfillment-block, pick-before-allocate, package-after-pick, dispatch guard, shipment transitions.

Integration tests (`IntegrationTests`, 21) against the LIVE LegacyRetailDB, each self-contained (creates its own isolated rows via `TestData`):
- OrderDesk: discount-approval routing + approve + convert; double-convert prevention; mandatory-info failure; staff cancellation allows Allocated (SQL rule); override requires authorized agent + writes audit.
- CustomerPortal: checkout creates order; checkout idempotency (same key -> same order, exactly 1 row); inventory recheck blocks oversell; coupon reduces total + records redemption; customer cancellation stricter (blocks Allocated); ownership enforced; return window.
- FulfillmentAPI: allocation reserves stock + blocks over-allocation; allocation blocked for cancelled; picking requires allocation; full allocate->pick->package->dispatch; carrier-event idempotency; return receipt restock + idempotency + approval requirement.
- Cross-app: Workflow C (portal request -> OrderDesk approve -> warehouse receive), Workflow D (portal cancel -> fulfillment allocation blocked), Workflow A (staff quote->order->fulfillment), cross-app shipment visibility (portal + OrderDesk read FulfillmentAPI data).

Run: `dotnet test LegacyRetailPortfolio.slnx` (integration tests require a provisioned DB; the fixture asserts availability).

### Phase 6 — Benchmark + docs  ✅ DONE

- `benchmark/ground-truth/`: application-inventory.md, business-capabilities.yaml,
  domain-entities.yaml, capability-overlaps.yaml, business-rules.yaml,
  cross-app-workflows.yaml, dependencies.yaml, evidence-map.yaml,
  modernization-opportunities.md, README.md (with "withhold from analyzing agent" notice).
  All evidence references point to real files/methods.
- `docs/ARCHITECTURE.md`: Mermaid diagrams (system context, layering, DB ownership,
  return sequence, warehouse state machine) + key design decisions.
- `scripts/`: provision-db.ps1 (+ -Reset), run-portal.ps1, run-fulfillment.ps1, run-orderdesk.ps1.
- `README.md`: prerequisites, container/LocalDB setup, quick start, dev credentials, seeded
  data summary, sample workflows, repo layout, testing.
- `.gitignore` added.

## Intentional-imperfection coverage (vs targets)
- Duplicated validation rules: 3 (mandatory-info, availability, commission).
- Semantically overlapping / different names: 2 (cancellation eligibility; stock check).
- SQL-enforced rules: 4 (allocate, receive-return, convert-quote, cancel-eligibility).
- Different C# models / same SQL table: 2 (Customers; Orders x3).
- Multi-app workflows: 3 (returns C, cancel D, staff-order A).
- Terminology inconsistencies: Order/CustomerOrder/FulfillmentOrder; Customer/CustomerAccount; Requested/Received.
- Bounded cancellation divergence: 1 ('Allocated').
- Complex cross-domain query: 1 (usp_WarehouseActivitySummary).

## FINAL STATUS
All 6 phases complete. Solution builds 0 errors / 0 warnings. 70 tests pass. All three
apps verified running against the live LegacyRetailDB. All five end-to-end workflows
exercised by integration tests.
