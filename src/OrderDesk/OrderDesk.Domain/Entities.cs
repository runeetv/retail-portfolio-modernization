namespace OrderDesk.Domain;

/* OrderDesk's OWN domain model. Even for shared tables (Customers, Products,
   Orders), OrderDesk defines its own entity shape with only the fields staff
   care about. These classes are intentionally NOT shared with CustomerPortal
   or FulfillmentAPI. Terminology note: OrderDesk calls the end party a
   "Customer" (vs CustomerPortal "CustomerAccount"). */

/// <summary>Staff-facing view of a customer (maps to shared dbo.Customers).</summary>
public class Customer
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

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>Mandatory information required before an order can be created.</summary>
    public bool HasMandatoryOrderInfo =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(ShipAddressLine);
}

/// <summary>Catalog product (maps to shared dbo.Products).</summary>
public class Product
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsDiscontinued { get; set; }
    public ProductCategory? Category { get; set; }
}

public class ProductCategory
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; }
}

/// <summary>Stock snapshot used for availability checks (maps to dbo.Inventory).</summary>
public class InventoryItem
{
    public int ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityAllocated { get; set; }
    public int ReorderLevel { get; set; }
    public int QuantityAvailable => QuantityOnHand - QuantityAllocated;
}

public enum QuoteStatus { Draft, PendingApproval, Approved, Rejected, Expired, Converted }

public class SalesAgent
{
    public int SalesAgentId { get; set; }
    public string AgentCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal CommissionRate { get; set; }
    public bool CanOverrideCancellation { get; set; }
    public bool IsActive { get; set; }
}

public class SalesQuote
{
    public int SalesQuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public int SalesAgentId { get; set; }
    public string QuoteStatus { get; set; } = OrderDesk.Domain.QuoteStatus.Draft.ToString();
    public decimal DiscountPercent { get; set; }
    public decimal SubTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public int? ConvertedOrderId { get; set; }

    public Customer? Customer { get; set; }
    public SalesAgent? SalesAgent { get; set; }
    public List<SalesQuoteLine> Lines { get; set; } = new();

    public bool IsExpired(DateTime utcNow) => ExpiresUtc < utcNow;
}

public class SalesQuoteLine
{
    public int SalesQuoteLineId { get; set; }
    public int SalesQuoteId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Product? Product { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class DiscountApproval
{
    public int DiscountApprovalId { get; set; }
    public int SalesQuoteId { get; set; }
    public decimal RequestedPercent { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public int? ApprovedByAgentId { get; set; }
    public DateTime? DecisionUtc { get; set; }
    public string? Notes { get; set; }
    public SalesQuote? SalesQuote { get; set; }
}

public class SalesCommission
{
    public int SalesCommissionId { get; set; }
    public int SalesAgentId { get; set; }
    public int OrderId { get; set; }
    public decimal OrderValue { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class SalesAgentTask
{
    public int SalesAgentTaskId { get; set; }
    public int SalesAgentId { get; set; }
    public string Title { get; set; } = "";
    public string? Details { get; set; }
    public int? RelatedCustomerId { get; set; }
    public int? RelatedQuoteId { get; set; }
    public string TaskStatus { get; set; } = "Open";
    public DateTime? DueUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Staff-facing order view (maps to shared dbo.Orders).</summary>
public class Order
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public string OrderStatus { get; set; } = "Pending";
    public string OrderSource { get; set; } = "OrderDesk";
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int? PlacedByAgentId { get; set; }
    public int? SourceQuoteId { get; set; }
    public string? CancelReason { get; set; }
    public DateTime PlacedUtc { get; set; }

    public Customer? Customer { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public Product? Product { get; set; }
}

public class CancellationOverride
{
    public int CancellationOverrideId { get; set; }
    public int OrderId { get; set; }
    public int SalesAgentId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string Justification { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Staff-facing return view (maps to shared dbo.Returns). OrderDesk reviews/approves.</summary>
public class ReturnRequest
{
    public int ReturnId { get; set; }
    public string ReturnNumber { get; set; } = "";
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public string ReturnStatus { get; set; } = "Requested";
    public string? Reason { get; set; }
    public DateTime RequestedUtc { get; set; }
    public int? ReviewedByAgentId { get; set; }
    public DateTime? ReviewedUtc { get; set; }
}

/// <summary>Read-only shipment status for customer-service lookups (owned by FulfillmentAPI).</summary>
public class ShipmentStatusView
{
    public int ShipmentId { get; set; }
    public int OrderId { get; set; }
    public string ShipmentStatus { get; set; } = "";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? DispatchedUtc { get; set; }
}
