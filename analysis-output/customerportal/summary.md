# CustomerPortal — Executive Summary (Reverse-Engineered)

## What business functions CustomerPortal implements
A customer-facing **ASP.NET Core MVC** self-service web application (.NET 10). Shoppers can
sign in, manage their profile, browse/search the catalog, build a cart, apply coupons, check
out (idempotently), view order history, cancel eligible orders, request returns, track
shipments, keep a wishlist, and raise support tickets. Persistence is EF Core over a shared
SQL Server database; the portal uses **no stored procedures**.

## How its domains are organized
Account & Identity, Self-Service Profile, Catalog Browsing & Availability, Shopping Cart &
Coupons, Checkout & Order Placement, Order History & Cancellation, Returns, Shipment Tracking
(read-only), Support Tickets, and Wishlist. (See `capabilities.yaml`, 18 capabilities across
10 domains.)

## What users can do and under which conditions
- **Browse the catalog anonymously**; everything else requires a cookie-authenticated session.
- **Check out** only with a non-empty cart, a profile that has an email + shipping address,
  and sufficient inventory at the moment of checkout; the operation is **idempotent** on a
  per-attempt key.
- **Cancel** an order only while it is `Pending` or `Confirmed` (CP-RULE-015).
- **Request a return** only for a `Delivered` order within **30 days**, and only one active
  return per order (CP-RULE-017/018).
- **Apply a coupon** only if it is active, unexpired, meets the cart minimum, matches the
  required customer status, and is under its redemption cap (CP-RULE-008).
- **Act only on their own records** — every order/return/ticket/wishlist/profile query is
  filtered by the signed-in identity, and all POST actions require an antiforgery token
  (CP-RULE-014, CP-RULE-023).

## Entities and database objects
- **Portal-owned tables:** CustomerAccounts, CustomerPreferences, ShoppingCarts,
  ShoppingCartItems, Wishlists, WishlistItems, Coupons, CouponRedemptions, SupportTickets,
  SupportTicketMessages.
- **Shared tables (read/write):** Customers, Orders, OrderLines, Returns, ReturnLines.
- **Shared tables (read-only):** Products, ProductCategories, Inventory.
- **External, read-only:** Shipments, CarrierEvents.
- The portal keeps **its own models over shared tables** — `CustomerAccount` + `ShopperProfile`
  over `dbo.Customers`, and `CustomerOrder`/`CustomerReturn` over `dbo.Orders`/`dbo.Returns`.

## Business rules that govern operations (highlights)
Idempotent checkout (CP-RULE-009), mandatory profile info + inventory recheck at checkout
(CP-RULE-010/011), coupon eligibility + discount computation (CP-RULE-008/012), customer
cancellation window (CP-RULE-015), single-active-return + 30-day return window
(CP-RULE-017/018), support-ticket transition graph (CP-RULE-021), and record-ownership +
antiforgery controls (CP-RULE-014/023). Full list in `business-rules.yaml`.

## Application-level dependencies
- Shared infra `LegacyRetail.Common` (connection string + PBKDF2 hashing).
- EF Core SqlServer; cookie authentication; antiforgery.
- **External, read-only data:** shipment tracking (`Shipments`/`CarrierEvents`) and inventory
  quantities are produced by processes outside the portal; order lifecycle beyond
  `Confirmed`/`Cancelled`, `OrderSource='OrderDesk'` orders, and return review/receipt
  transitions originate externally. These are recorded as dependencies without inferring
  what produces them (`dependencies.yaml`).

## What remains uncertain (see analysis-gaps.md)
- No portal code creates accounts/customers or writes preferences/coupons (provisioned
  externally).
- The portal creates returns but never transitions them; shipment/inventory data is
  read-only and externally maintained.
- The coupon redemption cap is enforced as a **global** count (no per-customer uniqueness).

## Verification status
Rules and core workflows were confirmed by executing the portal's own tests —
`CustomerPortal.Tests` (16/16 passed) and `CustomerPortalWorkflowTests` (6/6 passed) against
a provisioned `LegacyRetailDB`. Remaining findings are statically verified from inspected
source and SQL.
