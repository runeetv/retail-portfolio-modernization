# CustomerPortal — Application Profile (Reverse-Engineered)

> Reconstructed solely from CustomerPortal source (`src/CustomerPortal/**`), the shared
> infrastructure it references (`LegacyRetail.Common`), the SQL objects it uses, and
> CustomerPortal-specific tests. No portfolio ground-truth or other-application code was
> consulted. IDs cross-reference the YAML artifacts in this folder.

## 1. What CustomerPortal is

CustomerPortal is a **customer-facing ASP.NET Core MVC web application** (.NET 10,
`Microsoft.NET.Sdk.Web`) for **self-service online shopping and account management**.
Shoppers browse a product catalog, manage a cart, apply coupons, check out, view order
history, cancel eligible orders, request returns, track shipments, maintain a wishlist,
and raise support tickets. It persists to a SQL Server database (`LegacyRetailDB`) whose
schema header states it is shared by multiple applications; the portal maps the slice it
needs through its own `PortalDbContext` and uses **no stored procedures** — all data access
is EF Core LINQ.

## 2. Technical architecture

Four projects in a layered chain:

```
CustomerPortal.Web (ASP.NET Core MVC)        controllers + Razor views + Program.cs
      |            \         \        \
      v             v         v        v
CustomerPortal.Application  .Domain  .Infrastructure  Shared/LegacyRetail.Common
      (10 services)         (entities (PortalDbContext) (DbConnectionConfig,
                             + PortalRules)              PasswordHasher)
```

- **Startup (`Program.cs`):** registers `PortalDbContext` (SqlServer, `EnableRetryOnFailure`)
  and ten scoped application services; configures **cookie authentication**
  (`LoginPath=/Account/Login`, `AccessDeniedPath=/Account/Login`, 4-hour expiry),
  authorization, MVC with views (antiforgery on by default), and `HttpContextAccessor`.
  Default route `{controller=Home}/{action=Index}/{id?}`. A non-development exception
  handler maps to `/Home/Error`. `Program` is exposed as `public partial` for a test host.
- **Connection:** `DbConnectionConfig.Resolve(config.GetConnectionString("LegacyRetailDB"))`
  → environment variable `LEGACYRETAIL_CONNECTION` else a LocalDB default.
- **Identity:** `AuthService.SignInAsync` verifies a PBKDF2 hash
  (`LegacyRetail.Common.PasswordHasher.Verify`), rejects locked accounts, and updates
  `LastLoginUtc`. `AccountController.Login` issues a cookie principal with claims
  `Name(Username)`, `CustomerAccountId`, `CustomerId`, `CustomerStatus`, read back through
  the `CurrentUser` extension methods.
- **Authorization model:** `Catalog`, `Home`, and `Account` actions are anonymous (public
  browsing + login); every other controller is `[Authorize]`, and **every POST action
  carries `[ValidateAntiForgeryToken]`**. Business operations consistently pass the
  signed-in `User.CustomerId()` / `User.AccountId()` to services, which filter by it —
  record-level ownership enforcement layered on top of authentication.
- **Result pattern:** services return `ServiceResult` / `ServiceResult<T>` (success + error),
  which controllers surface via `TempData`/view models rather than throwing.

### MVC surface (controllers → services)

| Controller | Auth | Key actions | Services |
|------------|------|-------------|----------|
| Account | anon | Login (GET/POST), Logout (POST) | AuthService |
| Home | anon | Index, Error | — |
| Catalog | anon | Index (search + availability), Details | CatalogBrowseService |
| Cart | [Authorize] | Index, Add/Update/Remove, ApplyCoupon/RemoveCoupon | CartService |
| Checkout | [Authorize] | Index (issues idempotency key), Place | CartService, CheckoutService |
| Orders | [Authorize] | Index, Details, Cancel, RequestReturn | OrderHistoryService, ReturnService, TrackingService |
| Returns | [Authorize] | Index | ReturnService |
| Support | [Authorize] | Index, Details, Create, AddMessage, ChangeStatus | SupportTicketService |
| Wishlist | [Authorize] | Index, Add, Remove | WishlistService |
| Profile | [Authorize] | Index (GET/POST) | ProfileService |

## 3. Business domains

Account & Identity (CP-DOM-IDENTITY), Self-Service Profile (CP-DOM-PROFILE), Catalog
Browsing & Availability (CP-DOM-CATALOG), Shopping Cart & Coupons (CP-DOM-CART), Checkout &
Order Placement (CP-DOM-CHECKOUT), Order History & Cancellation (CP-DOM-ORDERS), Returns
(CP-DOM-RETURNS), Shipment Tracking (CP-DOM-TRACKING, read-only), Support Tickets
(CP-DOM-SUPPORT), and Wishlist (CP-DOM-WISHLIST).

## 4. How technical components implement the business

- **Checkout (CP-CAP-009)** is the centerpiece. `CheckoutService.CheckoutAsync` is
  **idempotent**: it first looks up an existing order by idempotency key and returns it if
  present; the final insert is also protected by the `UQ_Orders_Idempotency` filtered unique
  index, with a `DbUpdateException` fallback that returns the race winner. Before creating
  the order it enforces a non-empty cart, mandatory profile info (email + shipping address),
  and an **authoritative inventory recheck** per line. It computes any coupon discount,
  writes the `Order` (`Confirmed`/`CustomerPortal`) + lines, records a `CouponRedemption`
  when the discount is positive, and marks the cart `CheckedOut` — all inside an execution
  strategy transaction.
- **Coupons** are validated by `PortalRules.EvaluateCoupon` (active, not expired, cart
  minimum, required customer status, redemption cap) at apply time and re-validated at
  checkout; `PortalRules.CouponDiscount` computes a percent or fixed amount capped at the
  cart total.
- **Customer cancellation (CP-CAP-011)** is permitted only while an order is `Pending` or
  `Confirmed` (`PortalRules.CustomerCanCancel`), with ownership enforced and re-cancellation
  blocked.
- **Returns (CP-CAP-012)** can be requested only for a `Delivered` order within a 30-day
  window (`PortalRules.CanRequestReturn`), deduplicated against existing active returns, and
  created in `Requested` status by copying the order lines.
- **Tracking (CP-CAP-014)** reads externally-produced `Shipments`/`CarrierEvents` after an
  ownership check and maps raw statuses to friendly labels.
- **Support tickets** enforce a status-transition graph (`PortalRules.IsValidTicketTransition`)
  and block replies to Closed tickets.

## 5. Data model & persistence

CustomerPortal's 20 domain classes map to physical tables via `PortalDbContext`
(full table in `dependencies.yaml`):

- **Portal-owned tables** (defined in `database/schema/03_customerportal.sql`):
  CustomerAccounts, CustomerPreferences, ShoppingCarts, ShoppingCartItems, Wishlists,
  WishlistItems, Coupons, CouponRedemptions, SupportTickets, SupportTicketMessages.
- **Shared tables** (declared in `database/schema/01_schema.sql`): Customers, Products,
  ProductCategories, Inventory, Orders, OrderLines, Returns, ReturnLines.
- **External, read-only:** Shipments, CarrierEvents.

Notable: the portal defines **its own narrow models over shared tables** — `CustomerAccount`
(login, 1:1 to `dbo.Customers` via `CustomerId`) plus `ShopperProfile` (a second model over
`dbo.Customers`), and `CustomerOrder`/`CustomerReturn` over `dbo.Orders`/`dbo.Returns`.
DB constraints reinforce rules: `UQ_Orders_Idempotency` (checkout idempotency),
`UQ_CustomerAccounts_Username`/`UQ_CustomerAccounts_Customer`, `UQ_ShoppingCartItems`,
`UQ_WishlistItems`, `UQ_Coupons_Code`, and status CHECK constraints on carts, tickets, and
orders.

## 6. Verification performed

- `dotnet test tests/CustomerPortal.Tests` → **16/16 passed** (coupon eligibility/discount,
  customer cancellation set, return window, ticket transitions).
- `dotnet test` filtered to `CustomerPortalWorkflowTests` → **6/6 passed** against a
  provisioned `LegacyRetailDB`: checkout creates an order and is idempotent; checkout
  rechecks inventory and blocks when unavailable; coupon reduces the total and records a
  redemption; customer cancellation is stricter (blocks `Allocated`); ownership prevents
  cancelling another customer's order; returns only for delivered orders within the window.

Findings validated by these runs are `behavior_verified`; the rest are `static_verified`
from inspected source/SQL. Open questions are in `analysis-gaps.md`.
