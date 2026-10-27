# Application Inventory (Ground Truth)

> **Benchmark artifact.** Describes the portfolio as actually implemented. Keep this
> folder separate from the application source when running a reverse-engineering
> experiment — the analyzing agent should rediscover these facts from code + DDL.

## Portfolio: LegacyRetailPortfolio (.NET 10)

Three independently deployable applications sharing one SQL Server database
(`LegacyRetailDB`). Each app owns its own domain model, DbContext, application
services, and business rules. There is **no** shared business-logic/domain library;
only `LegacyRetail.Common` (connection-string + PBKDF2 hashing infrastructure) is shared.

| App | Id | Type | Primary responsibility | Entry project |
|-----|----|------|------------------------|---------------|
| OrderDesk | `APP-ORDERDESK` | WPF desktop (net10.0-windows, MVVM) | Internal staff sales & order management | `src/OrderDesk/OrderDesk.Desktop` |
| CustomerPortal | `APP-PORTAL` | ASP.NET Core MVC + Razor (net10.0) | Customer-facing self-service shopping | `src/CustomerPortal/CustomerPortal.Web` |
| FulfillmentAPI | `APP-FULFILL` | ASP.NET Core Web API (controllers, net10.0) | Warehouse execution, shipments, returns receipt | `src/FulfillmentAPI/FulfillmentAPI.Api` |

## Per-application project layout

Each app uses a 4-project split: `Domain` (entities + pure rules), `Infrastructure`
(EF Core DbContext over shared tables), `Application` (services/workflows), and a
presentation project (Desktop / Web / Api).

### OrderDesk (`APP-ORDERDESK`)
- `OrderDesk.Domain` — entities (`Customer`, `Product`, `Order`, `SalesQuote`, …) + `OrderDeskRules`.
- `OrderDesk.Infrastructure` — `OrderDeskDbContext`.
- `OrderDesk.Application` — `CustomerService`, `CatalogService`, `QuoteService`, `OrderService`, `ReturnReviewService`, `AgentTaskService`.
- `OrderDesk.Desktop` — WPF MVVM (tabs: Customers, Products, Quotations, Orders, Discount Approvals, Returns, Agent Tasks).

### CustomerPortal (`APP-PORTAL`)
- `CustomerPortal.Domain` — entities (`CustomerAccount`, `ShopperProfile`, `ShoppingCart`, `CustomerOrder`, …) + `PortalRules`.
- `CustomerPortal.Infrastructure` — `PortalDbContext`.
- `CustomerPortal.Application` — `AuthService`, `ProfileService`, `CatalogBrowseService`, `CartService`, `CheckoutService`, `OrderHistoryService`, `ReturnService`, `TrackingService`, `SupportTicketService`, `WishlistService`.
- `CustomerPortal.Web` — MVC controllers + Razor views, cookie auth, antiforgery.

### FulfillmentAPI (`APP-FULFILL`)
- `FulfillmentAPI.Domain` — entities (`FulfillmentOrder`, `Warehouse`, `PickList`, `Shipment`, …) + `FulfillmentRules`.
- `FulfillmentAPI.Infrastructure` — `FulfillmentDbContext`.
- `FulfillmentAPI.Application` — `FulfillmentQueryService`, `AllocationService`, `PickingService`, `ShipmentService`, `ReturnReceiptService`.
- `FulfillmentAPI.Api` — controllers, Swagger, API-key middleware, centralized error handling.

## Database provisioning
- `database/provisioning/DbProvisioner` owns schema creation + deterministic seeding.
- Applications query/update the shared schema but never modify its structure at runtime
  (no EF migrations are applied by the apps).

## Bounded contexts (logical ownership of shared tables)
- Shared: Customers, Products, ProductCategories, Orders, OrderLines, Inventory, Returns, ReturnLines.
- OrderDesk-owned: SalesQuotes, SalesQuoteLines, DiscountApprovals, SalesAgents, SalesAgentTasks, SalesCommissions, CancellationOverrides.
- CustomerPortal-owned: CustomerAccounts, ShoppingCarts, ShoppingCartItems, Wishlists, WishlistItems, SupportTickets, SupportTicketMessages, CustomerPreferences, Coupons, CouponRedemptions.
- FulfillmentAPI-owned: Warehouses, WarehouseBins, InventoryAllocations, PickLists, PickListItems, Shipments, ShipmentPackages, CarrierEvents, ReturnReceipts, WarehouseAdjustments.
