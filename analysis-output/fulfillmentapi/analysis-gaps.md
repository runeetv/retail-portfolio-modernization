# FulfillmentAPI — Analysis Gaps & Low-Confidence Findings

Records uncertainty and items not fully resolvable from the permitted materials.

## Gaps

### GAP-001 — `FulfillmentRules.CanCreatePickList` is defined but never called
The domain helper `CanCreatePickList(orderStatus)` (allows `Allocated`/`Picking`) exists but
`PickingService.CreatePickListAsync` does not invoke it; instead it gates on the order not
being cancelled plus the presence of `Allocated` allocations. The effective rule is captured
as FA-RULE-005/FA-RULE-006, but the unused helper suggests an intended status gate that is
not wired in.
- Impact: low (the implemented behavior is clear and tested).

### GAP-002 — Pick-rollup order-status expression is convoluted
In `MarkItemPickedAsync`, the line
`if (pick.PickStatus == "Completed") order.OrderStatus = "Packed" == order.OrderStatus ? order.OrderStatus : "Picking";`
is awkward: when the pick list completes it sets the order to `Picking` (unless already
`Packed`). Combined with the following line, the observed outcome is `Allocated -> Picking`.
No transition to a dedicated "picking complete" order state is performed; packaging later
moves the order to `Packed`. Documented as FA-RULE-013 with a note.
- Impact: low (outcome is deterministic; the order advances to Packed at packaging).

### GAP-003 — Orders / OrderLines / Returns / Inventory are shared and partly external
FulfillmentAPI reads orders (any `OrderSource`, with `PlacedByAgentId` set externally) and
returns (created + approved externally), and only mutates status/quantities within its remit.
Pre-fulfillment order states, return creation/approval, product master data, and base
inventory quantities originate outside this application (EXT-ORDERS, EXT-RETURNS,
EXT-PRODUCTS, EXT-INVENTORY). Their producers are not inferred.
- Impact: medium for whole-system understanding; the app's own writes are fully characterized.

### GAP-004 — `usp_WarehouseActivitySummary` reaches a non-FulfillmentAPI table
The activity-summary procedure joins `dbo.SalesAgents` (via `Orders.PlacedByAgentId`), a
table with no FulfillmentAPI entity and not defined in the fulfillment schema file. It is a
read-only reporting dependency (EXT-SALESAGENTS). Its DDL and owner were out of scope and
not inspected.
- Impact: low (reporting only).

### GAP-005 — InventoryAllocation / ReturnReceipt have unused CHECK states
`InventoryAllocations.AllocationStatus` permits `Released`/`Consumed` and
`ReturnReceipts.ReceivedStatus` permits `Inspected`, but no code path produces them.
Whether these are future-use, or driven by an unobserved process, is unresolved.
- Impact: low.

### GAP-006 — No per-shipment uniqueness; one-pick-list rule is app-level only
"One pick list per order" (FA-RULE-007) is enforced only in C# (no DB unique constraint on
`PickLists.OrderId`), so concurrent pick-list creation for the same order is not prevented at
the database level. Allocation and return-receipt concurrency are protected in SQL
(UPDLOCK/HOLDLOCK and UQ_ReturnReceipts_Return), but pick-list creation is not.
- Impact: medium for concurrency reasoning; consistent with the code as written.

### GAP-007 — No transaction spans multiple EF operations in picking/packaging
Allocation and return receipt are atomic inside their stored procedures. Picking, packaging,
and status updates use plain `SaveChanges` per operation with `EnableRetryOnFailure`
(no explicit multi-statement transaction). This is sufficient per single call but means a
multi-call sequence is not transactionally atomic.
- Impact: low–medium; matches the code.

## Confidence notes
- Findings marked `behavior_verified` were confirmed by executing `FulfillmentAPI.Tests`
  (18/18 passed) and `FulfillmentWorkflowTests` (6/6 passed) against a provisioned
  LegacyRetailDB on LocalDB. All other findings are `static_verified` from inspected
  source/SQL, except the external-dependency items, which are `static_verified` as
  dependencies while their producers remain uninspected by design.
- The HTTP API surface was traced from controllers into services; endpoints were not
  invoked live (the workflow tests exercise the services directly). Status-code mappings are
  `static_verified` from `FulfillmentControllerBase.FromResult` + `[ProducesResponseType]`.
- `Entities.cs` comments reference other applications; those were used only to classify this
  application's own reads/writes and external dependencies.
