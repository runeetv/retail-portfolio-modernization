# OrderDesk — Analysis Gaps & Low-Confidence Findings

This file records uncertainty, items that could not be fully resolved from the permitted
materials, and places where behavior was inferred rather than directly observed.

## Gaps

### GAP-001 — Staff-order creation has no observed UI entry point
`OrderService.CreateStaffOrderAsync` (OD-CAP-010, OD-WF-002) is fully implemented and
tested at the service level, but no `TabItem`/command in `MainWindow.xaml` invokes it.
The Orders tab supports listing, viewing, and cancelling — not creating. The capability is
real and reachable programmatically, but its intended UI trigger is unresolved.
- Impact: medium (capability exists; user-facing invocation path unclear).
- Evidence: src/OrderDesk/OrderDesk.Desktop/Views/MainWindow.xaml (no create-order command);
  src/OrderDesk/OrderDesk.Application/OrderService.cs:CreateStaffOrderAsync.

### GAP-002 — dbo.Shipments definition is external
OrderDesk reads `dbo.Shipments` (OD-CAP-016, OD-ENT-015) read-only, but the table is not
declared in the schema files that define OrderDesk's tables
(`database/schema/01_schema.sql`, `02_orderdesk.sql`). Its DDL and owning application were
out of scope and not inspected. Recorded as external dependency EXT-SHIPMENTS.
- Impact: low (clearly read-only from OrderDesk's perspective).

### GAP-003 — Return creation and later statuses are external
OrderDesk only transitions returns `Requested -> Approved/Rejected` (OD-CAP-013, OD-WF-004).
No OrderDesk code creates a `Requested` return, and OrderDesk never sets `Received`/`Closed`
(both allowed by `CK_Returns_Status`). These originate from an external source
(EXT-RETURNS-CREATE / EXT-RETURNS-RECEIVE). Not inferred further per scope rules.
- Impact: low–medium (OrderDesk's own stage is fully understood).
- Confidence of the "external" conclusion: inferred (based on absence of a code path).

### GAP-004 — SalesAgentTask creation path not found
`AgentTaskService` reads tasks and updates status (OD-CAP-014) but has no create method.
How tasks are created (external process, seed, or unobserved path) is unresolved.
- Impact: low.

### GAP-005 — SalesAgent lifecycle (create/update) not found
`SalesAgent` (OD-ENT-005) is only ever read within OrderDesk (agent lists, rate, override
flag). No create/update path was observed; agent records may be managed externally or via
seed/administration not present in the OrderDesk projects.
- Impact: low.

### GAP-006 — Authorization/identity is informal
There is no authentication or role model. The "acting agent" is a plain integer typed into
the Orders tab (`OrdersViewModel.ActingAgentId`, default 1), and approval/review decisions
take an agent id parameter. Whether an external mechanism authenticates staff is unknown;
within OrderDesk, agent identity is unauthenticated input.
- Impact: medium for security reasoning; the override authority check itself
  (`SalesAgent.CanOverrideCancellation`) is enforced server-side in the service.

### GAP-007 — Orders lifecycle beyond Confirmed/Cancelled is externally driven
`Orders.OrderStatus` permits Allocated/Picking/Packed/Shipped/Delivered
(`CK_Orders_Status`), but OrderDesk only creates `Confirmed` orders and sets `Cancelled`.
The intermediate fulfilment statuses are advanced by an external actor (EXT-ORDERS-OTHER-SOURCE).
OrderDesk's cancellation-eligibility rule nonetheless references `Allocated`, implying
awareness of that externally-set state.
- Impact: low for OrderDesk behavior; relevant to cross-system understanding (out of scope).

## Notes on confidence
- Findings marked `behavior_verified` were confirmed by executing the OrderDesk unit tests
  (15/15 passed) and the OrderDesk workflow integration tests (5/5 passed) against a
  provisioned LegacyRetailDB on LocalDB. All other findings are `static_verified` from
  inspected source/SQL, except the external-dependency inferences noted above.
- Code comments in the source reference other applications by name. Those references were
  used only to classify OrderDesk's own reads/writes and external dependencies; the
  functionality of other applications was not inferred.
