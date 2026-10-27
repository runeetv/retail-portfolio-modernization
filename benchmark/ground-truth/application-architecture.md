# Application Architecture (Ground Truth)

> **Benchmark artifact.** Derived from the other ground-truth files
> (`application-inventory.md`, `business-capabilities.yaml`, `capability-overlaps.yaml`,
> `domain-entities.yaml`). Describes each application's functionality, the features that
> overlap between applications, and the exact database tables each application uses.
> Keep this folder **withheld** from the analyzing agent during experiments.

---

## 1. Portfolio overview

Three independently deployable .NET 10 applications share one SQL Server database
(`LegacyRetailDB`). Each app owns its domain model, EF Core `DbContext`, services, and
business rules. The only shared code is `LegacyRetail.Common` (connection-string +
PBKDF2 hashing infrastructure — no business logic, no entities).

### The applications at a glance

- **OrderDesk** (`APP-ORDERDESK`) — an internal **WPF desktop** application (MVVM) used by
  sales reps and customer-service staff. It is the back-office tool for the sales side of
  the business: searching and editing customers, building sales quotations, approving
  manual discounts, converting approved quotes into orders, placing staff-assisted orders,
  cancelling orders (with an authorized-override audit trail), reviewing customer returns,
  and tracking agent tasks and commissions. Several of its rules are enforced in SQL
  stored procedures.

- **CustomerPortal** (`APP-PORTAL`) — a customer-facing **ASP.NET Core MVC** web
  application for self-service shopping. Customers sign in, browse and search the catalog,
  manage a shopping cart, apply coupons, check out (idempotently), view their order
  history, request cancellations and returns, follow shipment tracking in a friendly view,
  manage a wishlist, and raise support tickets. It enforces its own, generally stricter,
  customer-facing rules in C#.

- **FulfillmentAPI** (`APP-FULFILL`) — a warehouse-facing **ASP.NET Core Web API**
  (controllers + Swagger, API-key auth) that executes physical fulfilment: listing pending
  orders, allocating inventory, generating pick lists and recording picking, packaging and
  dispatching shipments, recording carrier tracking events, receiving returned merchandise
  (with restock), and reporting cross-domain warehouse activity. It processes orders from
  **both** OrderDesk and CustomerPortal and owns the shipment and allocation data the other
  apps read.

Together they form a single retail business process that spans the three apps: a sale is
created in OrderDesk or CustomerPortal, fulfilled by FulfillmentAPI, and any return travels
Portal → OrderDesk → FulfillmentAPI.

```mermaid
flowchart TB
    subgraph Actors
        Staff["Sales / CS staff"]
        Shopper["Customer"]
        WH["Warehouse ops / carriers"]
    end

    subgraph Apps["LegacyRetailPortfolio"]
        OD["OrderDesk\nWPF desktop (MVVM)\nAPP-ORDERDESK"]
        CP["CustomerPortal\nASP.NET Core MVC\nAPP-PORTAL"]
        FA["FulfillmentAPI\nASP.NET Core Web API\nAPP-FULFILL"]
    end

    Common["LegacyRetail.Common\n(infra utils only)"]
    DB[("LegacyRetailDB\nSQL Server")]

    Staff --> OD
    Shopper --> CP
    WH --> FA

    OD --> Common
    CP --> Common
    FA --> Common

    OD -->|OrderDeskDbContext| DB
    CP -->|PortalDbContext| DB
    FA -->|FulfillmentDbContext| DB
```

---

## 2. Database tables used by each application

`LegacyRetailDB` is one physical schema. Tables are **logically** owned by an app, but
some are read and/or written across app boundaries. Legend:
**O** = owns/primary writer · **W** = writes · **R** = reads.

```mermaid
flowchart TB
    subgraph SHARED["Shared tables"]
        Customers; Products; ProductCategories; Orders; OrderLines; Inventory; Returns; ReturnLines
    end
    subgraph OD_T["OrderDesk-owned"]
        SalesQuotes; SalesQuoteLines; DiscountApprovals; SalesAgents; SalesAgentTasks; SalesCommissions; CancellationOverrides
    end
    subgraph CP_T["CustomerPortal-owned"]
        CustomerAccounts; ShoppingCarts; ShoppingCartItems; Wishlists; WishlistItems; SupportTickets; SupportTicketMessages; CustomerPreferences; Coupons; CouponRedemptions
    end
    subgraph FA_T["FulfillmentAPI-owned"]
        Warehouses; WarehouseBins; InventoryAllocations; PickLists; PickListItems; Shipments; ShipmentPackages; CarrierEvents; ReturnReceipts; WarehouseAdjustments
    end

    OD(["OrderDesk"]) --> OD_T
    CP(["CustomerPortal"]) --> CP_T
    FA(["FulfillmentAPI"]) --> FA_T
    OD --> SHARED
    CP --> SHARED
    FA --> SHARED
```

### OrderDesk — tables used
*DbContext: `OrderDesk.Infrastructure.OrderDeskDbContext` · model: `src/OrderDesk/OrderDesk.Domain/Entities.cs`*

| Table | Access | C# model | Notes |
|-------|--------|----------|-------|
| Customers | R/W | `Customer` | Full staff contact + shipping edit |
| Products | R | `Product` | |
| ProductCategories | R | `ProductCategory` | |
| Inventory | R | `InventoryItem` | Pre-order availability check |
| Orders | R/W | `Order` | Staff orders, cancellation |
| OrderLines | R/W | `OrderLine` | |
| Returns | R/W | `ReturnRequest` | Approve / reject |
| Shipments | R | `ShipmentStatusView` | Read-only (FulfillmentAPI owns) |
| SalesQuotes | **O** | `SalesQuote` | |
| SalesQuoteLines | **O** | `SalesQuoteLine` | |
| DiscountApprovals | **O** | `DiscountApproval` | |
| SalesAgents | **O** | `SalesAgent` | |
| SalesAgentTasks | **O** | `SalesAgentTask` | |
| SalesCommissions | **O** | `SalesCommission` | Written by `usp_ConvertQuoteToOrder` |
| CancellationOverrides | **O** | `CancellationOverride` | Override audit trail |

### CustomerPortal — tables used
*DbContext: `CustomerPortal.Infrastructure.PortalDbContext` · model: `src/CustomerPortal/CustomerPortal.Domain/Entities.cs`*

| Table | Access | C# model | Notes |
|-------|--------|----------|-------|
| Customers | R/W | `ShopperProfile` | Self-service subset (2nd model over this table) |
| Products | R | `CatalogProduct` | |
| ProductCategories | R | `Category` | |
| Inventory | R | `StockLevel` | Display + checkout recheck |
| Orders | R/W | `CustomerOrder` | Checkout, self cancellation (2nd model over this table) |
| OrderLines | R/W | `CustomerOrderLine` | |
| Returns | R/W | `CustomerReturn` | Create requests |
| ReturnLines | R/W | `CustomerReturnLine` | |
| Shipments | R | `TrackedShipment` | Read-only tracking |
| CarrierEvents | R | `TrackingEvent` | Read-only tracking |
| CustomerAccounts | **O** | `CustomerAccount` | Login identity (1:1 with Customers) |
| CustomerPreferences | **O** | `CustomerPreference` | |
| ShoppingCarts | **O** | `ShoppingCart` | |
| ShoppingCartItems | **O** | `ShoppingCartItem` | |
| Wishlists | **O** | `Wishlist` | |
| WishlistItems | **O** | `WishlistItem` | |
| Coupons | **O** | `Coupon` | |
| CouponRedemptions | **O** | `CouponRedemption` | |
| SupportTickets | **O** | `SupportTicket` | |
| SupportTicketMessages | **O** | `SupportTicketMessage` | |

### FulfillmentAPI — tables used
*DbContext: `FulfillmentAPI.Infrastructure.FulfillmentDbContext` · model: `src/FulfillmentAPI/FulfillmentAPI.Domain/Entities.cs`*

| Table | Access | C# model | Notes |
|-------|--------|----------|-------|
| Orders | R/W | `FulfillmentOrder` | Status rolls forward during fulfillment (3rd model over this table) |
| OrderLines | R | `FulfillmentOrderLine` | |
| Inventory | R/W | `StockRecord` | Allocation + restock |
| Returns | R/W | `ReturnForReceipt` | Receipt sets status `Received` |
| Customers | R | — (shipping fields) | Reads shipping address only |
| Warehouses | **O** | `Warehouse` | |
| WarehouseBins | **O** | `WarehouseBin` | |
| InventoryAllocations | **O** | `InventoryAllocation` | Written by `usp_AllocateInventory` |
| PickLists | **O** | `PickList` | |
| PickListItems | **O** | `PickListItem` | |
| Shipments | **O** | `Shipment` | |
| ShipmentPackages | **O** | `ShipmentPackage` | |
| CarrierEvents | **O** | `CarrierEvent` | |
| ReturnReceipts | **O** | `ReturnReceipt` | Written by `usp_ReceiveReturnAndAdjust` |
| WarehouseAdjustments | **O** | `WarehouseAdjustment` | |

---

## 3. Overlapping features

The same capability matrix at a glance — which app does what for each shared/overlapping
concept, and the overlap type:

| Scenario | OrderDesk | CustomerPortal | FulfillmentAPI | Overlap type |
|----------|-----------|----------------|----------------|--------------|
| Customer management | Full contact edit | Self-service subset | Reads shipping only | broadly shared |
| Order creation | Staff order / quote convert | Cart checkout | — | partial |
| Cancellation | SQL rule (allows `Allocated`) + override | C# rule (`Pending`/`Confirmed` only) | Blocks cancelled orders | partial, different impl |
| Inventory | Pre-order check | Display + checkout recheck | Physical allocation | partial, different impl |
| Returns | Approve / reject | Request | Receive + restock | partial (3-app process) |
| Shipment visibility | CS read-only | Friendly display | Owns data | broadly shared |

Overlap is partial and deliberate. The same concept is handled by different apps with
different responsibilities and, in several cases, different implementations
(see `capability-overlaps.yaml`).

```mermaid
flowchart TB
    S1["<b>1. Customer management</b>  (broadly shared)<br/>OrderDesk: full contact edit<br/>Portal: self-service subset<br/>Fulfillment: reads shipping only"]
    S2["<b>2. Order creation</b>  (partial overlap)<br/>OrderDesk: staff order / quote convert<br/>Portal: cart checkout<br/>Fulfillment: —"]
    S3["<b>3. Cancellation</b>  (partial overlap — different impl)<br/>OrderDesk: SQL rule, allows Allocated + override<br/>Portal: C# rule, Pending/Confirmed only<br/>Fulfillment: blocks processing if Cancelled"]
    S4["<b>4. Inventory</b>  (partial overlap — different impl)<br/>OrderDesk: pre-order check<br/>Portal: display + checkout recheck<br/>Fulfillment: physical allocation"]
    S5["<b>5. Returns lifecycle</b>  (partial overlap — 3-app process)<br/>Portal: request<br/>OrderDesk: approve / reject<br/>Fulfillment: receive + restock"]
    S6["<b>6. Shipment visibility</b>  (broadly shared)<br/>Fulfillment: owns data<br/>Portal: friendly display<br/>OrderDesk: CS read-only"]

    S1 --> S2 --> S3 --> S4 --> S5 --> S6
```

> Each block lists the three applications' responsibilities for that capability, stacked
> top-to-bottom. The arrows only order the scenarios for readability; they are not data flow.

---

## 4. Application functionality (block diagram)

Each application is shown as its own vertical column of capabilities.

**OrderDesk (`APP-ORDERDESK`) — internal staff**

```mermaid
flowchart TB
    OD(["OrderDesk"])
    OD --> OD1["Customer search + full contact edit"]
    OD1 --> OD2["Product catalog + pre-order stock check"]
    OD2 --> OD3["Sales quotations + line items"]
    OD3 --> OD4["Manual discounts + approval workflow"]
    OD4 --> OD5["Quote → Order conversion (stored proc)"]
    OD5 --> OD6["Staff-assisted orders"]
    OD6 --> OD7["Cancellation w/ authorized override + audit"]
    OD7 --> OD8["Return review (approve / reject)"]
    OD8 --> OD9["Agent tasks + commissions"]
    OD9 --> OD10["Shipment status (read-only)"]
```

**CustomerPortal (`APP-PORTAL`) — self-service web**

```mermaid
flowchart TB
    CP(["CustomerPortal"])
    CP --> CP1["Local auth (login)"]
    CP1 --> CP2["Profile self-service (subset)"]
    CP2 --> CP3["Browse / search catalog + availability"]
    CP3 --> CP4["Shopping cart + quantities"]
    CP4 --> CP5["Coupon application"]
    CP5 --> CP6["Idempotent checkout"]
    CP6 --> CP7["Order history + self cancellation"]
    CP7 --> CP8["Return requests"]
    CP8 --> CP9["Shipment tracking (friendly view)"]
    CP9 --> CP10["Support tickets"]
    CP10 --> CP11["Wishlist"]
```

**FulfillmentAPI (`APP-FULFILL`) — warehouse**

```mermaid
flowchart TB
    FA(["FulfillmentAPI"])
    FA --> FA1["Pending fulfillment orders"]
    FA1 --> FA2["Product availability"]
    FA2 --> FA3["Inventory allocation (stored proc)"]
    FA3 --> FA4["Pick lists + bin assignment + picking"]
    FA4 --> FA5["Shipment packaging"]
    FA5 --> FA6["Shipment status transitions + dispatch"]
    FA6 --> FA7["Carrier events (idempotent) + tracking"]
    FA7 --> FA8["Return receipt + restock (stored proc)"]
    FA8 --> FA9["Warehouse activity summary (cross-domain)"]
```

> The arrows stack each app's capabilities into a single vertical column for readability;
> they indicate a typical usage order, not strict data flow.

---

## 5. Tables with multiple C# models (intentional)

| Physical table | OrderDesk | CustomerPortal | FulfillmentAPI |
|----------------|-----------|----------------|----------------|
| `Customers` | `Customer` | `ShopperProfile` (+ `CustomerAccount` 1:1) | — (reads shipping) |
| `Orders` | `Order` | `CustomerOrder` | `FulfillmentOrder` |

These different models over one table — along with naming differences
(`Order`/`CustomerOrder`/`FulfillmentOrder`, `Customer`/`CustomerAccount`) — are
documented as intentional benchmark traps in `evidence-map.yaml`.

---

## 6. Cross-application table dependencies (summary)

```mermaid
flowchart LR
    CP["CustomerPortal"] -->|creates Returns 'Requested'| Returns[(Returns)]
    OD["OrderDesk"] -->|sets Returns 'Approved'| Returns
    FA["FulfillmentAPI"] -->|sets Returns 'Received' + restocks| Returns
    FA -->|owns| Shipments[(Shipments)]
    CP -->|reads tracking| Shipments
    OD -->|reads status| Shipments
    OD -->|quote → order| Orders[(Orders)]
    CP -->|checkout| Orders
    FA -->|allocate / pick / ship| Orders
```

See `dependencies.yaml` for the full code + stored-procedure dependency map and
`cross-app-workflows.yaml` for the end-to-end workflows these dependencies enable.
