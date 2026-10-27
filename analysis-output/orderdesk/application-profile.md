# OrderDesk — Application Profile (Reverse-Engineered)

> Reconstructed solely from OrderDesk source (`src/OrderDesk/**`), the shared infrastructure
> it references (`LegacyRetail.Common.DbConnectionConfig`), the SQL objects it uses, and
> OrderDesk-specific tests. No portfolio ground-truth or other-application code was consulted.
> IDs cross-reference `capabilities.yaml`, `entities.yaml`, `business-rules.yaml`,
> `workflows.yaml`, `dependencies.yaml`, `evidence-index.yaml`.

## 1. What OrderDesk is

OrderDesk is an **internal Windows desktop application** (WPF, .NET 10, `net10.0-windows`,
`OutputType=WinExe`) for **staff-driven retail sales and order management**. The window
title in `MainWindow.xaml` states its own purpose: *"OrderDesk — Internal Sales & Order
Management."* Staff (sales agents / customer-service) use it to manage customers, build and
approve sales quotations, place and cancel orders, review returns, and track agent tasks and
commissions. It persists to a SQL Server database named `LegacyRetailDB`, which is explicitly
a **single schema shared with other applications** (per the comment header in
`database/schema/01_schema.sql`); OrderDesk maps only the slice it needs.

## 2. Technical architecture

Four projects in a clean layered dependency chain:

```
OrderDesk.Desktop (WPF, WinExe)      UI: App + MainWindow + 7 tab ViewModels (MVVM)
      |          \        \
      v           v        v
OrderDesk.Application  OrderDesk.Domain  OrderDesk.Infrastructure
      |                                        |
      +----------------> OrderDesk.Domain      +--> Shared/LegacyRetail.Common
                                               +--> EF Core SqlServer + Microsoft.Data.SqlClient
```

- **Entry point / runtime:** `App.xaml` sets `StartupUri="Views/MainWindow.xaml"`.
  `MainWindow` instantiates `MainViewModel` as its `DataContext` and calls
  `MainViewModel.InitializeAsync()` on `Loaded` (OD-WF-005). A global
  `DispatcherUnhandledException` handler in `App.xaml.cs` shows a MessageBox and marks the
  exception handled.
- **Composition root:** `AppServices` is a hand-rolled singleton (`AppServices.Current`) —
  no IoC container. It builds the six application services, each receiving a
  `Func<OrderDeskDbContext>` factory so every operation uses a fresh, short-lived context.
- **Data access:** `OrderDeskDbContext` (EF Core, SQL Server). Most reads/writes use EF;
  two business operations drop to raw ADO (`Microsoft.Data.SqlClient`) to call stored
  procedures. `OrderDeskContextFactory` configures the context with `EnableRetryOnFailure`,
  and transactional writes are wrapped in `Database.CreateExecutionStrategy()` for retry
  compatibility.
- **Connection:** `LegacyRetail.Common.DbConnectionConfig.Resolve()` returns the
  `LEGACYRETAIL_CONNECTION` environment variable if set, otherwise a LocalDB default
  (`Server=(localdb)\MSSQLLocalDB;Database=LegacyRetailDB`).
- **Authentication/authorization:** none. This is an internal tool; the "acting agent" is an
  integer entered in the UI (`OrdersViewModel.ActingAgentId`, default `1`). The only
  authority check in code is `SalesAgent.CanOverrideCancellation`, enforced in
  `OrderService.CancelOrderAsync`.
- **Error handling / transactions:** services return an `OperationResult` / `OperationResult<T>`
  (success + message) rather than throwing to the UI; SQL `THROW`s from the conversion proc
  are caught and surfaced as failure messages. Writes that span multiple rows
  (staff order; conversion proc) are transactional.

### UI surface (navigation)
`MainWindow.xaml` is a `TabControl` with seven tabs, each bound to its own ViewModel built on
a small MVVM base (`ObservableObject`, `AsyncRelayCommand`):

| Tab | ViewModel | Primary service | Capabilities |
|-----|-----------|-----------------|--------------|
| Customers | CustomersViewModel | CustomerService | OD-CAP-001, OD-CAP-002 |
| Products | ProductsViewModel | CatalogService | OD-CAP-003, OD-CAP-004 |
| Quotations | QuotesViewModel | QuoteService | OD-CAP-005..009 |
| Orders | OrdersViewModel | OrderService | OD-CAP-011, OD-CAP-012, OD-CAP-016 |
| Discount Approvals | ApprovalsViewModel | QuoteService | OD-CAP-008 |
| Returns | ReturnsViewModel | ReturnReviewService | OD-CAP-013 |
| Agent Tasks | TasksViewModel | AgentTaskService | OD-CAP-014, OD-CAP-015 |

## 3. Business domains

1. **Customer Management (staff)** — search and full contact/address editing (OD-DOM-CUSTOMER).
2. **Product Catalog & Inventory Availability** — browse active products, check stock (OD-DOM-CATALOG).
3. **Sales Quotation & Discount Approval** — the richest domain: quote lifecycle, discount
   threshold/approval, conversion to order (OD-DOM-QUOTING).
4. **Order Management (staff-assisted)** — create, view, and cancel orders with an authorized
   override path (OD-DOM-ORDER).
5. **Return Review** — approve/reject customer-requested returns (OD-DOM-RETURNS).
6. **Sales Agent Operations** — agent tasks and commission visibility (OD-DOM-AGENT).
7. **Shipment Status Lookup** — read-only tracking for customer service (OD-DOM-SHIPMENT).

## 4. How technical components implement the business

- **Quotation → order** is the core flow. `QuoteService` creates a Draft quote with an expiry
  (OD-RULE-001), merges line items and recomputes totals (OD-RULE-012/013), and applies a
  discount that either auto-approves or routes to a `DiscountApproval` based on a **15%
  threshold** (OD-RULE-003). Approval decisions (OD-CAP-008) flip the quote to Approved/Rejected.
  Conversion (`ConvertToOrderAsync`) first pre-checks convertibility and mandatory customer
  info in C#, then delegates the authoritative work to the stored procedure
  **`usp_ConvertQuoteToOrder`**, which (transactionally, with `UPDLOCK/HOLDLOCK`) re-validates
  status/expiry/convert-once/mandatory-info (THROW 50011–50014), writes the `Order` (status
  `Confirmed`, `OrderSource='OrderDesk'`) and its lines (with a per-line discount, OD-RULE-008),
  marks the quote `Converted`, and inserts a `SalesCommission` using
  **commission = GrandTotal × rate, halved when discount ≥ 20%** (OD-RULE-009).
- **Cancellation** (`OrderService.CancelOrderAsync`) asks the stored procedure
  **`usp_EvaluateCancellationEligibility`** whether the order is eligible (Pending/Confirmed/
  Allocated → eligible, OD-RULE-014). If not eligible, an agent with
  `CanOverrideCancellation` and a justification may force it, writing a `CancellationOverride`
  audit row before setting the order `Cancelled` (OD-RULE-015). This override-with-audit is a
  distinctive OrderDesk capability.
- **Inventory availability** is computed as `QuantityOnHand − QuantityAllocated` and used both
  for display and as a pre-order guard in staff order creation (OD-RULE-007).
- **Returns**: OrderDesk owns only the review step; it transitions `Requested → Approved/Rejected`
  and records the reviewer (OD-RULE-018). Creation and warehouse receipt happen elsewhere.

## 5. Data model & persistence

OrderDesk's domain model (15 classes in `OrderDesk.Domain/Entities.cs`) maps to physical
tables via `OrderDeskDbContext`. The app defines its own entity shapes even for shared tables.
Full mapping and access levels are in `dependencies.yaml`; highlights:

- **Logically OrderDesk-owned** (defined in `database/schema/02_orderdesk.sql`): SalesAgents,
  SalesQuotes, SalesQuoteLines, DiscountApprovals, SalesCommissions, SalesAgentTasks,
  CancellationOverrides.
- **Shared** (defined in `database/schema/01_schema.sql`): Customers, Products,
  ProductCategories, Orders, OrderLines, Inventory, Returns.
- **External, read-only:** `dbo.Shipments` (projected as `ShipmentStatusView`).
- **Stored procedures executed:** `usp_ConvertQuoteToOrder`, `usp_EvaluateCancellationEligibility`.

Database constraints reinforce business rules: `UQ_SalesCommissions_Order` (one commission per
order), filtered unique `UQ_SalesQuotes_ConvertedOrder` (a quote converts to at most one order),
`CK_SalesAgents_Rate` (0–1), `CK_Inventory_Alloc` (allocated ≤ on-hand), and status CHECK
constraints on quotes, orders, returns, and tasks.

## 6. Verification performed

- `dotnet test tests/OrderDesk.Tests` → **15/15 passed** (rule unit tests: cancellation
  eligibility set, discount threshold, convertibility, commission halving, mandatory-info).
- `dotnet test` filtered to `OrderDeskWorkflowTests` → **5/5 passed** against a provisioned
  `LegacyRetailDB`: discount-approval routing then conversion, double-convert prevention,
  missing-info blocks conversion, Allocated is staff-cancellable, override requires an
  authorized agent and writes an audit row.

Findings validated by these runs are marked `behavior_verified`; the rest are
`static_verified` from inspected source/SQL. Open questions are in `analysis-gaps.md`.
