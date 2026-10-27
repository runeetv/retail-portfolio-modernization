# OrderDesk — Executive Summary (Reverse-Engineered)

## Application purpose (reconstructed from code)
OrderDesk is an **internal WPF desktop application (.NET 10, Windows)** for **staff-driven
retail sales and order management**. Sales and customer-service staff use it to manage
customers, build and approve sales quotations, place and cancel orders, review returns, and
track sales-agent tasks and commissions. It reads and writes a shared SQL Server database
(`LegacyRetailDB`), mapping only the tables it needs through its own `OrderDeskDbContext`.
Evidence: `MainWindow.xaml` title "Internal Sales & Order Management"; `OrderDesk.Desktop`
(`WinExe`, `net10.0-windows`, `UseWPF`); the six application services.

## Primary business domains
- Customer management (staff, full contact edit)
- Product catalog & inventory availability
- Sales quotation & discount approval  ← core
- Order management (staff-assisted, with authorized cancellation override)
- Return review (approve/reject)
- Sales-agent operations (tasks & commissions)
- Shipment status lookup (read-only)

## Most important business capabilities
- **OD-CAP-009 Convert approved quotation to order** — transactional, via
  `usp_ConvertQuoteToOrder`; enforces approval/expiry/convert-once/mandatory-info and records
  commission. (behavior_verified)
- **OD-CAP-007/008 Apply discount + approve** — 15% threshold routes high discounts to an
  approval record. (behavior_verified)
- **OD-CAP-012 Cancel order with authorized override** — SQL-evaluated eligibility plus an
  override path gated by agent authority and written to an audit table. (behavior_verified)
- **OD-CAP-010 Create staff-assisted order** — direct order creation with stock + customer
  validation. (static_verified; UI trigger unresolved — see gaps)
- **OD-CAP-013 Review returns** — approve/reject `Requested` returns.

## Distinctive, application-specific functionality
- Sales **quotations** with line items, discount thresholds, and an **approval workflow**.
- **Quote-to-order conversion** that also computes **sales commission** (halved for aggressive
  discounts ≥ 20%).
- **Authorized cancellation override with an audit record** (`CancellationOverrides`) — lets
  specific agents cancel orders that are otherwise ineligible, with a mandatory justification.
- **Sales-agent tasks and commission** tracking.

## Important business rules (see business-rules.yaml)
- Discounts > 15% require approval (OD-RULE-003).
- A quote converts only when Approved, not expired, not already converted; enforced in both
  C# and SQL (OD-RULE-005/006).
- Order creation/conversion blocked when the customer lacks Email + shipping address —
  duplicated in C# and SQL (OD-RULE-010).
- Commission = order value × agent rate, halved when discount ≥ 20% (OD-RULE-009).
- Staff cancellation eligible for Pending/Confirmed/Allocated; otherwise requires an
  authorized override + audit (OD-RULE-014/015).
- Only `Requested` returns may be reviewed (OD-RULE-018).

## Database dependencies (see dependencies.yaml)
- **Writes (logically OrderDesk-owned):** SalesQuotes, SalesQuoteLines, DiscountApprovals,
  SalesCommissions (via proc), SalesAgentTasks (status), CancellationOverrides.
- **Reads/writes (shared):** Customers, Orders, OrderLines, Returns.
- **Reads (shared):** Products, ProductCategories, Inventory, SalesAgents.
- **Reads (external, read-only):** `dbo.Shipments`.
- **Stored procedures executed:** `usp_ConvertQuoteToOrder`, `usp_EvaluateCancellationEligibility`.

## Unresolved questions (see analysis-gaps.md)
- No UI entry point invokes staff-order creation (`OrderService.CreateStaffOrderAsync`) — the
  capability exists only at the service layer (GAP-001).
- `dbo.Shipments` is read but defined outside OrderDesk's schema files — external owner not
  inspected (GAP-002).
- Return creation (`Requested`) and later `Received`/`Closed` statuses are produced by an
  external actor, not OrderDesk (GAP-003).
- No creation path observed for `SalesAgentTask` or `SalesAgent` within OrderDesk (GAP-004/005).
- No authentication/role model; the acting agent is unauthenticated UI input (GAP-006).

## Verification status
Business rules and core workflows were confirmed by executing OrderDesk's own tests:
`OrderDesk.Tests` (15/15 passed) and `OrderDeskWorkflowTests` (5/5 passed) against a
provisioned `LegacyRetailDB`. Remaining findings are statically verified from inspected
source and SQL.
