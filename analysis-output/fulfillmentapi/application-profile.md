# FulfillmentAPI — Application Profile (Reverse-Engineered)

> Reconstructed solely from FulfillmentAPI source (`src/FulfillmentAPI/**`), the shared
> infrastructure it references (`LegacyRetail.Common.DbConnectionConfig`), the SQL stored
> procedures and DDL it uses, and FulfillmentAPI-specific tests. No portfolio ground-truth or
> other-application code was consulted. IDs cross-reference the YAML artifacts and
> `state-machines.md` in this folder.

## 1. What FulfillmentAPI is

FulfillmentAPI is a **controller-based ASP.NET Core Web API** (.NET 10,
`Microsoft.NET.Sdk.Web`) that performs **warehouse execution for order fulfillment**:
inventory allocation, picking, shipment packaging and dispatch, carrier tracking, return
receipt with inventory adjustment, and warehouse activity reporting. It is a
machine-to-machine service (API-key secured, Swagger-documented) that processes orders it
does **not** create — orders and returns originate outside the application, and it advances
their fulfillment-related state. Persistence is the shared SQL Server database
(`LegacyRetailDB`) via its own `FulfillmentDbContext`, with three stored procedures carrying
the transaction-critical logic.

## 2. Technical architecture

Four projects in a layered chain:

```
FulfillmentAPI.Api (Web API + Swagger + middleware)
      |          \         \        \
      v           v         v        v
FulfillmentAPI.Application  .Domain  .Infrastructure  Shared/LegacyRetail.Common
   (5 services + ApiResult) (entities (FulfillmentDbContext) (DbConnectionConfig)
                             + FulfillmentRules)
```

- **Startup (`Program.cs`):** registers `FulfillmentDbContext` (SqlServer,
  `EnableRetryOnFailure`) and five scoped services (`FulfillmentQueryService`,
  `AllocationService`, `PickingService`, `ShipmentService`, `ReturnReceiptService`);
  controllers; and Swagger with an **API-key security scheme** (`X-Api-Key`) plus XML
  comments. Connection resolved via `DbConnectionConfig.Resolve(config.GetConnectionString("LegacyRetailDB"))`
  → `LEGACYRETAIL_CONNECTION` env var else LocalDB. `Program` is `public partial` for a
  test host.
- **Pipeline order:** `ExceptionHandlingMiddleware` → Swagger/SwaggerUI → `ApiKeyMiddleware`
  → `GET /health` (minimal) + `MapControllers`.
- **Security:** `ApiKeyMiddleware` requires header `X-Api-Key` equal to `config["ApiKey"]`
  (default `dev-fulfillment-key`) on all requests except `/swagger*`, `/`, `/health`,
  `/openapi*`; otherwise 401 JSON. There is no per-user identity or role model
  (FA-RULE-023).
- **Error handling:** `ExceptionHandlingMiddleware` converts unhandled exceptions to a
  `500 application/problem+json` (RFC7807) body and logs them. Services return
  `ApiResult<T>` with an `ApiErrorKind` (`Validation`/`NotFound`/`Conflict`), and
  `FulfillmentControllerBase.FromResult` maps these to `400`/`404`/`409` (FA-RULE-024).
- **Transactions / concurrency:** the two mutating stored procedures use `XACT_ABORT` with
  `UPDLOCK/HOLDLOCK` (allocation) and idempotency guards (return receipt). EF operations
  (picking/packaging/status) use per-call `SaveChanges` with retry-on-failure.

### API surface (controllers → services)

All controllers are `[ApiController]`, routed under `api/fulfillment/*`, `[Produces("application/json")]`,
and delegate to services via `FromResult`.

| Controller | Endpoints | Service |
|------------|-----------|---------|
| Orders | GET `orders/pending`, GET `orders/{orderId}` | FulfillmentQueryService |
| Inventory | GET `inventory/availability/{productId}`, POST `inventory/allocate`, GET `inventory/allocations/{orderId}` | FulfillmentQueryService, AllocationService |
| Picking | POST `picking/picklists`, GET `picking/picklists/{id}`, POST `picking/items/{id}/pick` | PickingService |
| Shipments | POST `shipments`, PUT `shipments/{id}/status`, POST `shipments/{id}/events`, GET `shipments/{id}/tracking`, GET `shipments/by-order/{orderId}` | ShipmentService, FulfillmentQueryService |
| Returns | POST `returns/receive`, GET `returns/receipts/{id}/adjustments` | ReturnReceiptService |
| Warehouses | GET `warehouses`, GET `warehouses/activity-summary` | FulfillmentQueryService |

## 3. Business domains

Fulfillment Order Intake (read), Inventory Availability & Allocation, Picking, Shipment
Packaging/Dispatch/Tracking, Return Receipt & Inventory Adjustment, and Warehouse Reference
& Activity Reporting. (13 capabilities — see `capabilities.yaml`.)

## 4. How technical components implement the business

- **Allocation (FA-CAP-003)** calls `usp_AllocateInventory`, which — under `UPDLOCK/HOLDLOCK`
  on Orders and Inventory — rejects cancelled orders (THROW 50021) and over-allocation
  (THROW 50022), increments `Inventory.QuantityAllocated`, inserts `InventoryAllocations`,
  and advances a Pending/Confirmed order to `Allocated`. It is idempotent (skips
  already-allocated products).
- **Picking (FA-CAP-004/005)** requires prior allocation and a non-cancelled order, creates
  one pick list per order with round-robin bin assignment, then rolls pick-list status
  (Created→InProgress→Completed) and advances the order to `Picking` as items are picked.
- **Shipping (FA-CAP-006/007/008)** packages an order only after all items are picked (order
  → `Packed`), enforces a shipment transition graph, requires a package + carrier reference
  to dispatch (order → `Shipped`), and records carrier events idempotently on
  `(ShipmentId, EventCode, EventUtc)` (backed by `UQ_CarrierEvents`).
- **Return receipt (FA-CAP-010)** calls `usp_ReceiveReturnAndAdjust`, which accepts only
  `Approved` returns (THROW 50031), is idempotent (one receipt per return), restocks
  `Inventory.QuantityOnHand`, writes `WarehouseAdjustments`, and sets the return to
  `Received` — explicitly **not** touching any refund/financial state.
- **Reporting (FA-CAP-013)** runs `usp_WarehouseActivitySummary`, a cross-domain aggregate
  that joins fulfillment tables with shared `Orders`/`Customers` and the externally-owned
  `SalesAgents` table, classifying orders as staff vs web by `OrderSource`.

## 5. Data model & persistence

14 domain entities map to physical tables via `FulfillmentDbContext` (full table in
`dependencies.yaml`). Highlights:

- **Fulfillment-owned (created/maintained here):** InventoryAllocations, PickLists,
  PickListItems, Shipments, ShipmentPackages, CarrierEvents, ReturnReceipts,
  WarehouseAdjustments; plus the Warehouses/WarehouseBins reference data.
- **Shared tables it mutates narrowly:** Orders (OrderStatus roll-forward), Inventory
  (allocation + restock), Returns (→ Received).
- **Shared/external, read-only:** OrderLines, Products, Customers, ReturnLines, SalesAgents.

The app keeps **its own warehouse-centric model** over shared tables — notably
`FulfillmentOrder` over `dbo.Orders` (the class comment calls this a deliberate naming
difference). Idempotency and integrity are backed by DB constraints: `UQ_CarrierEvents`,
`UQ_ReturnReceipts_Return`, `UQ_ShipmentPackages_Code`, `CK_Inventory_Alloc`, and the status
CHECK constraints on allocations/pick lists/shipments/receipts.

## 6. State machines

Reconstructed in `state-machines.md`: Order (fulfillment subset
Confirmed→Allocated→Picking→Packed→Shipped→Delivered), InventoryAllocation (only `Allocated`
produced), PickList (Created→InProgress→Completed), PickListItem (picked flag), Shipment
(Packed→Dispatched→InTransit→Delivered, `*→Exception`), Return (Approved→Received only), and
ReturnReceipt (`Restocked`). States allowed by CHECK constraints but never produced here are
flagged rather than assumed active.

## 7. Verification performed

- `dotnet test tests/FulfillmentAPI.Tests` → **18/18 passed** (rule unit tests:
  fulfillment-block, pick-before-allocate helper, package-after-pick, dispatch guard,
  shipment transition graph).
- `dotnet test` filtered to `FulfillmentWorkflowTests` → **6/6 passed** against a provisioned
  `LegacyRetailDB`: allocation reserves stock + blocks over-allocation; allocation blocked
  for cancelled order; picking requires prior allocation; full allocate→pick→package→dispatch
  (dispatch without carrier → 409; with carrier → success → order Shipped); carrier-event
  idempotency; return receipt restocks + is idempotent + requires Approved.

Findings validated by these runs are `behavior_verified`; the rest are `static_verified`.
Open questions are in `analysis-gaps.md`.
