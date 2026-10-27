# LegacyRetailPortfolio — Architecture

Three independently developed .NET 10 applications over one shared SQL Server
database (`LegacyRetailDB`). Each application owns its domain model, DbContext,
services, and business rules. Only infrastructure utilities are shared.

## System context

```mermaid
flowchart TB
    subgraph Users
        Staff["Sales / CS staff"]
        Shopper["Customer"]
        WH["Warehouse systems / ops"]
    end

    OD["OrderDesk\n(WPF desktop, MVVM)"]
    CP["CustomerPortal\n(ASP.NET Core MVC)"]
    FA["FulfillmentAPI\n(ASP.NET Core Web API)"]

    DB[("LegacyRetailDB\n(SQL Server)")]

    Staff --> OD
    Shopper --> CP
    WH --> FA

    OD -->|OrderDeskDbContext| DB
    CP -->|PortalDbContext| DB
    FA -->|FulfillmentDbContext| DB

    CP -. reads shipment/tracking .-> DB
    OD -. reads shipment status .-> DB
```

## Per-application layering

```mermaid
flowchart LR
    subgraph OrderDesk
        OD_D[Domain] --> OD_I[Infrastructure]
        OD_D --> OD_A[Application]
        OD_I --> OD_A
        OD_A --> OD_UI[Desktop WPF]
    end
    subgraph CustomerPortal
        CP_D[Domain] --> CP_I[Infrastructure]
        CP_D --> CP_A[Application]
        CP_I --> CP_A
        CP_A --> CP_UI[Web MVC]
    end
    subgraph FulfillmentAPI
        FA_D[Domain] --> FA_I[Infrastructure]
        FA_D --> FA_A[Application]
        FA_I --> FA_A
        FA_A --> FA_UI[Api]
    end
    Common["LegacyRetail.Common\n(infra utils only)"]
    OD_I --> Common
    CP_I --> Common
    FA_I --> Common
```

## Shared database — table ownership

```mermaid
flowchart TB
    subgraph Shared
        Customers
        Products
        ProductCategories
        Orders
        OrderLines
        Inventory
        Returns
        ReturnLines
    end
    subgraph OrderDesk-owned
        SalesQuotes
        SalesQuoteLines
        DiscountApprovals
        SalesAgents
        SalesAgentTasks
        SalesCommissions
        CancellationOverrides
    end
    subgraph Portal-owned
        CustomerAccounts
        ShoppingCarts
        ShoppingCartItems
        Wishlists
        Coupons
        CouponRedemptions
        SupportTickets
    end
    subgraph Fulfillment-owned
        Warehouses
        InventoryAllocations
        PickLists
        Shipments
        CarrierEvents
        ReturnReceipts
        WarehouseAdjustments
    end
    Orders --> OrderLines
    Orders --> Shipments
    Orders --> InventoryAllocations
    Returns --> ReturnReceipts
    CustomerAccounts --> Customers
```

## Cross-application return workflow (Workflow C)

```mermaid
sequenceDiagram
    participant C as Customer (Portal)
    participant O as Staff (OrderDesk)
    participant W as Warehouse (FulfillmentAPI)
    participant DB as LegacyRetailDB

    C->>DB: RequestReturnAsync -> Returns('Requested')
    O->>DB: ReviewAsync(approve) -> Returns('Approved')
    W->>DB: usp_ReceiveReturnAndAdjust
    DB-->>W: ReturnReceipt + restock Inventory
    Note over DB: Returns('Received') — NOT a refund
```

## Warehouse fulfillment (Workflow E)

```mermaid
stateDiagram-v2
    [*] --> Confirmed
    Confirmed --> Allocated: usp_AllocateInventory
    Allocated --> Picking: PickList created + items picked
    Picking --> Packed: CreateShipmentWithPackage
    Packed --> Shipped: UpdateStatus(Dispatched) + carrier ref
    Shipped --> Delivered: carrier events
    Confirmed --> Cancelled: cancellation
    Allocated --> Cancelled: staff override only
```

## Key design decisions
- **No shared domain library.** Each app re-declares overlapping concepts (Customer,
  Order) with its own shape. See `benchmark/ground-truth/domain-entities.yaml`.
- **Rules split between SQL and C#** intentionally (e.g. staff cancellation in a stored
  procedure, customer cancellation in a C# rule) to create realistic divergence.
- **DbProvisioner owns schema**; applications never run migrations at runtime.
- **Idempotency** on checkout (idempotency key + filtered unique index), carrier events
  (unique key), and return receipts (one-per-return guard).
