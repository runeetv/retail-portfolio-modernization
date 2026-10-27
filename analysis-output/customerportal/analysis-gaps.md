# CustomerPortal — Analysis Gaps & Low-Confidence Findings

Records uncertainty and items not fully resolvable from the permitted materials.

## Gaps

### GAP-001 — Returns are created but never transitioned by the portal
`ReturnService` creates returns with status `Requested` and reads them, but no portal
code advances a return's status. The `CK_Returns_Status` set includes
`Approved/Rejected/Received/Closed`, so those transitions are driven by external actors
(EXT-RETURNS-TRANSITION). What performs them is not inferred.
- Impact: low–medium. The portal's own stage (request) is fully understood.
- Confidence of "external" conclusion: inferred (absence of a code path).

### GAP-002 — Shipment / carrier-event data is external and read-only
`TrackingService` reads `dbo.Shipments` and `dbo.CarrierEvents`, but these tables are not
declared in the portal/shared schema files inspected (`01_schema.sql`, `03_customerportal.sql`),
and no portal code writes them. They are produced by an external application
(EXT-SHIPMENTS / EXT-CARRIEREVENTS). Their DDL and producer were out of scope.
- Impact: low (clearly read-only from the portal).

### GAP-003 — Orders table is shared; portal sees states it does not set
The portal creates `Confirmed`/`CustomerPortal` orders and can set `Cancelled`. Order
detail and cancel/return eligibility logic reference later statuses
(`Allocated`, `Delivered`, etc.) that the portal never writes, and `OrderSource` allows
`OrderDesk`. Those originate externally (EXT-ORDERS-OTHER-SOURCE). Not inferred further.
- Impact: low for portal behavior; relevant to cross-system understanding (out of scope).

### GAP-004 — CustomerAccount / Customer creation and preference writes not observed
No portal code creates `CustomerAccount`, creates/updates `CustomerPreferences`, or creates
the shared `Customers` row. `AuthService` reads accounts; `ProfileService.GetPreferencesAsync`
only reads preferences. Account provisioning appears to happen outside the portal (e.g. an
administrative or seeding process not present in the portal projects).
- Impact: medium (registration/self-signup is absent from the portal surface).

### GAP-005 — Inventory is read-only to the portal
The portal reads `dbo.Inventory` for display and the checkout recheck but never writes it.
Stock changes (allocation, replenishment) are performed by an external process
(EXT-INVENTORY-UPDATES). The checkout recheck narrows but does not eliminate a race between
the recheck and order creation (no stock decrement/allocation is done by the portal).
- Impact: medium for correctness reasoning; consistent with the code as written.

### GAP-006 — Agent-authored support replies are external
Portal code authors ticket messages with `Author='Customer'`. The schema comment indicates
the `Author` column may hold an agent name, implying support staff reply via an external
mechanism (EXT-SUPPORT-AGENT) not present in the portal.
- Impact: low.

### GAP-007 — Coupon definitions are read-only to the portal
`Coupons` are read for eligibility and discount, but no portal code creates or edits
coupons. Coupon administration is external/seeded. Redemption-cap enforcement counts
`CouponRedemptions` but there is no per-customer uniqueness constraint, so the cap is a
global count across all customers (as implemented).
- Impact: low–medium (documents the exact semantics of the cap).

## Confidence notes
- Findings marked `behavior_verified` were confirmed by executing `CustomerPortal.Tests`
  (16/16 passed) and `CustomerPortalWorkflowTests` (6/6 passed) against a provisioned
  LegacyRetailDB on LocalDB. All other findings are `static_verified` from inspected
  source/SQL, except the external-dependency items noted as `inferred`.
- Entities.cs contains comments referencing other applications. Those were used only to
  classify the portal's own reads/writes and external dependencies; no other application's
  behavior was inferred.
- Razor views were enumerated and key forms sampled; the business behavior was traced
  through controllers and services rather than the view markup.
