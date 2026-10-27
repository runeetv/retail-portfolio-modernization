# Data Consolidation Assessment — LegacyRetailPortfolio (planning)

Assessment of the existing `LegacyRetailDB` (SQL Server / LocalDB) for the consolidation. Guiding
principle (from the brief): **minimize unnecessary database changes** in the initial consolidation;
change only where there is a clear benefit.

Evidence: Stage 2 `canonical-domains.yaml` + `cross-app-dependencies.yaml`, Stage 1 entity catalogs,
`database/procedures/procedures.sql`, `module-ownership-matrix.yaml`. Tier: **[verified]** / **[proposed]**.

## 1. Headline recommendation

[proposed] **Keep the existing schema as-is for phase 1.** All 40 tables and all 5 stored procedures
are retained unchanged. The consolidation is primarily an *application-layer* merge (three apps → one
modular monolith over the same DB), not a data migration. This is the lowest-risk path and is supported
by the fact that the three apps already share one physical schema today. [verified: single `LegacyRetailDB`,
three `DbContext`s mapping only what each needs.]

## 2. Tables retained (all 40)

[verified from `domain-entities.yaml`]

- **Shared (8):** Customers, Products, ProductCategories, Orders, OrderLines, Inventory, Returns, ReturnLines.
- **OrderDesk-owned (7):** SalesQuotes, SalesQuoteLines, DiscountApprovals, SalesAgents, SalesAgentTasks,
  SalesCommissions, CancellationOverrides.
- **CustomerPortal-owned (10):** CustomerAccounts, ShoppingCarts, ShoppingCartItems, Wishlists,
  WishlistItems, SupportTickets, SupportTicketMessages, CustomerPreferences, Coupons, CouponRedemptions.
- **FulfillmentAPI-owned (15):** Warehouses, WarehouseBins, InventoryAllocations, PickLists, PickListItems,
  Shipments, ShipmentPackages, CarrierEvents, ReturnReceipts, WarehouseAdjustments.

All retained. **No table dropped.**

## 3. Tables potentially merged — assessment

[proposed] **None merged in phase 1.** The candidates a naive consolidation might merge are exactly the
multi-model tables, and the evaluation (Stage 3) confirmed these are intentional distinctions:

| Candidate | Why it looks mergeable | Why NOT to merge |
|-----------|------------------------|------------------|
| `Customers` + `CustomerAccounts` | 1:1 by CustomerId | Distinct concepts (customer record vs login identity); merging destroys the TERM-2 distinction and complicates auth. Keep 1:1. |
| The three Order models over `Orders` | Same table | Already one table; only the C# models differed. Unify at the aggregate layer, not the schema. |
| `Returns` stage ownership | Three apps touch it | One table already; lifecycle unifies in MOD-RETURNS without schema change. |

Any table merge is deferred to a possible later optimization phase with its own risk assessment.

## 4. Schema changes required

[proposed] For phase 1: **none mandatory.** Optional, low-risk, benefit-justified only:
- (Optional) Add a role/claims table **only if** the new role taxonomy (DEC-ROLES) is stored in the DB
  rather than the identity provider. Deferred pending DEC-ROLES.
- No column renames, no type changes, no new FKs required to run the consolidated app.

## 5. Migration risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Two DbContexts mapping the same table with divergent configs cause EF conflicts | Medium | Medium | Decide persistence approach (DEC-PERSISTENCE): single DbContext with module configs, or per-module DbContexts with non-overlapping writable mappings. |
| Stored-proc transactions split if logic moved to C# | Low (if procs kept) | High | Keep procs verbatim in phase 1; do not reimplement in C#. |
| Connection-string / env override (`LEGACYRETAIL_CONNECTION`) drift across former apps | Medium | Low | Single connection config via LegacyRetail.Common.DbConnectionConfig. |
| Idempotency/unique constraints lost in refactor | Low | High | Preserve UQ_Orders_Idempotency, UQ_CarrierEvents, UQ_ReturnReceipts_Return, UQ_SalesCommissions_Order, UQ_SalesQuotes_ConvertedOrder, UQ_Customers_Email, CK_Inventory_Alloc. |
| OrderSource semantics broken | Low | Medium | OrderSource must stay the staff/web discriminator (drives FA-RULE-022 reporting). |

## 6. Data ownership (post-consolidation)

[proposed] Single owning module per table (see `module-ownership-matrix.yaml#table_ownership`). The
former cross-app shared-write tables and their write authorities:
- `Orders`/`OrderLines` → MOD-ORDER (capture/cancel), MOD-QUOTING via proc, MOD-FULFILLMENT status roll-forward.
- `Inventory` → MOD-FULFILLMENT (allocation/restock) only; others read.
- `Returns`/`ReturnLines` → MOD-RETURNS across all stages.
- `CouponRedemptions` → MOD-SHOPPING, also written in the checkout transaction (MOD-ORDER).

## 7. Referential integrity constraints to preserve

[verified] Keep all existing constraints: FK relationships among Orders/OrderLines/Customers/Products,
`CK_Inventory_Alloc` (QuantityAllocated ≤ QuantityOnHand), `CK_Orders_Source` (OrderDesk|CustomerPortal),
`CK_Orders_Status`, `CK_SupportTickets_Status`, `CK_SalesAgentTasks_Status`, `CK_PickListItems_Qty`,
`CK_ShoppingCartItems_Qty`, and all unique indexes listed in §5. These encode business rules and must
survive consolidation.

## 8. Stored-procedure dependencies

[verified] Five procedures, all retained as-is:

| Proc | Owner module | Touches | Notes |
|------|--------------|---------|-------|
| `usp_ConvertQuoteToOrder` | MOD-QUOTING | SalesQuotes, SalesQuoteLines, Orders, OrderLines, SalesCommissions, Customers, SalesAgents | Quote→order+commission in one tx; convert-once; mandatory-info THROW 50014. |
| `usp_EvaluateCancellationEligibility` | MOD-ORDER | Orders | Staff cancel eligibility. |
| `usp_AllocateInventory` | MOD-FULFILLMENT | Orders, OrderLines, Inventory, InventoryAllocations | No over-allocation (50022), block cancelled (50021), XACT_ABORT. |
| `usp_ReceiveReturnAndAdjust` | MOD-RETURNS | Returns, ReturnLines, ReturnReceipts, Inventory, WarehouseAdjustments | Approved-only (50031), restock, idempotent, no refund. |
| `usp_WarehouseActivitySummary` | MOD-REPORTING | Warehouses, PickLists, InventoryAllocations, Orders, Shipments, Customers, SalesAgents | Cross-domain read; becomes intra-app after consolidation. |

[proposed] The procs remain the authoritative transaction boundaries. Modules invoke them through their
owning module's service; no cross-module code reaches into another module's proc directly.

## 9. Data access changes

[proposed]
- From three EF Core `DbContext`s (`OrderDeskDbContext`, `PortalDbContext`, `FulfillmentDbContext`) to a
  consolidated persistence approach (DEC-PERSISTENCE). The connection target is unchanged.
- No raw SQL other than the existing stored-proc calls is introduced.
- Repository/service boundaries follow module ownership so that writes to a shared table go through its
  owning module.

## 10. Historical data preservation

[proposed] Because the schema is unchanged and the same database is reused, **all historical data is
preserved in place** — no ETL, no data copy, no backfill required for phase 1. Orders retain their
`OrderSource`, returns retain their lifecycle status, shipments/carrier events and audit rows
(CancellationOverrides, WarehouseAdjustments, SalesCommissions) are untouched.

## 11. Open items / decisions

- **DEC-PERSISTENCE** [decision] single DbContext (module-scoped configs) vs retained per-module DbContexts.
  Recommendation: one DbContext with per-module `IEntityTypeConfiguration` groupings, to avoid duplicate
  mappings of shared tables; alternative kept in `architecture-options.md`.
- **DEC-ROLES** [decision] whether role/permission data lives in the DB (new table) or identity layer.
- **OPEN-Q-SEED** [gap] base `Products`/`ProductCategories`/`SalesAgents`/`Inventory` base rows are
  populated by `DbProvisioner`/seed, not by any app (Stage 3 H-2). Confirm the authoritative owner of
  master data post-consolidation (likely an admin/reference module — out of phase-1 scope).
