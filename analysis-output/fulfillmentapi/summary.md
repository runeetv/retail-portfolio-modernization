# FulfillmentAPI — Executive Summary (Reverse-Engineered)

## What business functions FulfillmentAPI performs
A controller-based **ASP.NET Core Web API** (.NET 10) that executes **warehouse fulfillment**
for orders it does not create: it checks availability, allocates inventory, generates pick
lists and records picking, packages and dispatches shipments, records carrier tracking
events, receives approved returns (restocking inventory), and reports warehouse activity. It
is a machine-to-machine service secured by an API key and documented with Swagger/OpenAPI;
persistence is the shared SQL Server database via its own DbContext and three stored
procedures.

## Operational domains
Fulfillment Order Intake (read-only), Inventory Availability & Allocation, Picking, Shipment
Packaging/Dispatch/Tracking, Return Receipt & Inventory Adjustment, and Warehouse Reference &
Activity Reporting. (13 capabilities — `capabilities.yaml`.)

## Business entities and their lifecycles
- **Created/maintained by this app:** InventoryAllocation, PickList/PickListItem,
  Shipment/ShipmentPackage/CarrierEvent, ReturnReceipt, WarehouseAdjustment (plus
  Warehouse/WarehouseBin reference data).
- **Shared records it mutates narrowly:** `FulfillmentOrder` (advances `Orders.OrderStatus`),
  `StockRecord` (`Inventory` allocation + restock), `ReturnForReceipt` (`Returns` → Received).
- **Read-only / external:** OrderLines, Products, Customers, ReturnLines, SalesAgents.
- Lifecycles are detailed in `state-machines.md` (Order, Allocation, PickList, Shipment,
  Return, ReturnReceipt).

## Rules governing operations (highlights)
- Cannot allocate beyond available stock; cannot allocate a cancelled order (SQL:
  `usp_AllocateInventory`, THROW 50022/50021 — FA-RULE-003/004).
- Picking requires prior allocation; one pick list per order (FA-RULE-005/007).
- Items must be picked before packaging; dispatch requires a package + carrier reference
  (FA-RULE-009/016).
- Shipment status follows a fixed transition graph; `*→Exception` always allowed
  (FA-RULE-015).
- Carrier events are idempotent on (shipment, code, time); return receipt is idempotent per
  return (FA-RULE-018/021).
- Only **Approved** returns may be received; receipt restocks inventory and sets `Received`
  but does **not** change refund state (FA-RULE-019/020).
- API key required on all non-exempt endpoints (FA-RULE-023).

## State transitions enforced
Order advances Confirmed→Allocated→Picking→Packed→Shipped→Delivered (fulfillment subset);
Shipment Packed→Dispatched→InTransit→Delivered with `*→Exception`; PickList
Created→InProgress→Completed; Return Approved→Received. Invalid shipment transitions and
non-Approved return receipts are rejected with 409. (Full diagrams in `state-machines.md`.)

## Database dependencies
- **Fulfillment-owned tables:** Warehouses, WarehouseBins, InventoryAllocations, PickLists,
  PickListItems, Shipments, ShipmentPackages, CarrierEvents, ReturnReceipts,
  WarehouseAdjustments.
- **Shared tables mutated:** Orders (status), Inventory (quantities), Returns (status).
- **Shared/external read-only:** OrderLines, Products, Customers, ReturnLines, SalesAgents.
- **Stored procedures:** `usp_AllocateInventory`, `usp_ReceiveReturnAndAdjust`,
  `usp_WarehouseActivitySummary` (bodies inspected; see `dependencies.yaml`).

## External business information consumed
Orders and order lines (any origin; `PlacedByAgentId` set externally), base inventory
quantities, product master data, returns (created + approved externally), and the
`SalesAgents` table (read only inside the activity-summary join). These are recorded as
dependencies (EXT-ORDERS, EXT-INVENTORY, EXT-RETURNS, EXT-PRODUCTS, EXT-CUSTOMERS,
EXT-SALESAGENTS) without inferring what produces them.

## What remains uncertain (analysis-gaps.md)
- `FulfillmentRules.CanCreatePickList` is defined but unused (GAP-001); the pick-rollup
  order-status expression is convoluted (GAP-002).
- Several CHECK-allowed states are never produced (allocation Released/Consumed; receipt
  Inspected) (GAP-005).
- "One pick list per order" is enforced only in C# (no DB uniqueness), unlike allocation and
  return-receipt which are protected in SQL (GAP-006).
- Pre-fulfillment order/return states, product/inventory master data, and the SalesAgents
  table are external and uninspected by design (GAP-003/004).

## Verification status
Rules and workflows were confirmed by executing the application's own tests:
`FulfillmentAPI.Tests` (18/18 passed) and `FulfillmentWorkflowTests` (6/6 passed) against a
provisioned `LegacyRetailDB`. Remaining findings are statically verified from inspected
source and SQL.
