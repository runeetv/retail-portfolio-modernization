namespace CustomerPortal.Domain;

/* CustomerPortal's OWN domain model. Intentionally NOT shared with OrderDesk or
   FulfillmentAPI. Terminology note: the portal's central identity is the
   "CustomerAccount" (login), which is a DIFFERENT model than OrderDesk's
   "Customer" even though both ultimately relate to the shared dbo.Customers row.
   This is one of the intentional "different C# models -> same/related SQL table"
   cases for the reverse-engineering benchmark. */

/// <summary>Portal login identity (maps to dbo.CustomerAccounts).</summary>
public class CustomerAccount
{
    public int CustomerAccountId { get; set; }
    public int CustomerId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsLocked { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedUtc { get; set; }

    // The portal's view of the shared customer profile (maps to dbo.Customers).
    public ShopperProfile? Profile { get; set; }
}

/// <summary>
/// Portal's self-service view of the shared customer record (maps to dbo.Customers).
/// This is a SECOND C# representation of the same table OrderDesk calls "Customer"
/// - intentionally narrower (the portal exposes a smaller editable subset).
/// </summary>
public class ShopperProfile
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? ShipAddressLine { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipRegion { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipCountry { get; set; }
    public string CustomerStatus { get; set; } = "Standard";
}

public class CustomerPreference
{
    public int CustomerPreferenceId { get; set; }
    public int CustomerAccountId { get; set; }
    public string PrefKey { get; set; } = "";
    public string PrefValue { get; set; } = "";
}

public class CatalogProduct
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsDiscontinued { get; set; }
    public Category? Category { get; set; }
}

public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; }
}

public class StockLevel
{
    public int ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityAllocated { get; set; }
    public int Available => QuantityOnHand - QuantityAllocated;
}

public class ShoppingCart
{
    public int ShoppingCartId { get; set; }
    public int CustomerAccountId { get; set; }
    public string CartStatus { get; set; } = "Active";
    public string? AppliedCouponCode { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? CheckedOutUtc { get; set; }
    public List<ShoppingCartItem> Items { get; set; } = new();
}

public class ShoppingCartItem
{
    public int ShoppingCartItemId { get; set; }
    public int ShoppingCartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedUtc { get; set; }
    public CatalogProduct? Product { get; set; }
}

public class Wishlist
{
    public int WishlistId { get; set; }
    public int CustomerAccountId { get; set; }
    public string Name { get; set; } = "My Wishlist";
    public DateTime CreatedUtc { get; set; }
    public List<WishlistItem> Items { get; set; } = new();
}

public class WishlistItem
{
    public int WishlistItemId { get; set; }
    public int WishlistId { get; set; }
    public int ProductId { get; set; }
    public DateTime AddedUtc { get; set; }
    public CatalogProduct? Product { get; set; }
}

public class Coupon
{
    public int CouponId { get; set; }
    public string Code { get; set; } = "";
    public string CouponType { get; set; } = "Percent"; // Percent | FixedAmount
    public decimal Value { get; set; }
    public decimal MinCartTotal { get; set; }
    public string? RequiredStatus { get; set; }
    public int? MaxRedemptions { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public bool IsActive { get; set; }
}

public class CouponRedemption
{
    public int CouponRedemptionId { get; set; }
    public int CouponId { get; set; }
    public int CustomerAccountId { get; set; }
    public int? OrderId { get; set; }
    public decimal AmountDiscounted { get; set; }
    public DateTime RedeemedUtc { get; set; }
}

public class SupportTicket
{
    public int SupportTicketId { get; set; }
    public int CustomerAccountId { get; set; }
    public string Subject { get; set; } = "";
    public string TicketStatus { get; set; } = "Open";
    public int? RelatedOrderId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<SupportTicketMessage> Messages { get; set; } = new();
}

public class SupportTicketMessage
{
    public int SupportTicketMessageId { get; set; }
    public int SupportTicketId { get; set; }
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Portal's view of an order (maps to shared dbo.Orders). Third distinct order model.</summary>
public class CustomerOrder
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public string OrderStatus { get; set; } = "Pending";
    public string OrderSource { get; set; } = "CustomerPortal";
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CancelReason { get; set; }
    public DateTime PlacedUtc { get; set; }
    public List<CustomerOrderLine> Lines { get; set; } = new();
}

public class CustomerOrderLine
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public CatalogProduct? Product { get; set; }
}

/// <summary>Portal's view of a return (maps to shared dbo.Returns). Customer creates these.</summary>
public class CustomerReturn
{
    public int ReturnId { get; set; }
    public string ReturnNumber { get; set; } = "";
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public string ReturnStatus { get; set; } = "Requested";
    public string? Reason { get; set; }
    public DateTime RequestedUtc { get; set; }
    public List<CustomerReturnLine> Lines { get; set; } = new();
}

public class CustomerReturnLine
{
    public int ReturnLineId { get; set; }
    public int ReturnId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Read-only shipment + tracking data OWNED by FulfillmentAPI (Scenario 6).</summary>
public class TrackedShipment
{
    public int ShipmentId { get; set; }
    public int OrderId { get; set; }
    public string ShipmentStatus { get; set; } = "";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? DispatchedUtc { get; set; }
    public List<TrackingEvent> Events { get; set; } = new();
}

public class TrackingEvent
{
    public int CarrierEventId { get; set; }
    public int ShipmentId { get; set; }
    public string EventCode { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventUtc { get; set; }
}
