namespace FulfillmentAPI.Domain;

/// <summary>
/// FulfillmentAPI's own warehouse rules. Some overlap conceptually with the other
/// apps but are logistics-specific. Allocation/receipt rules that touch inventory
/// are enforced in SQL stored procedures (usp_AllocateInventory,
/// usp_ReceiveReturnAndAdjust); these C# helpers cover sequencing/eligibility.
/// </summary>
public static class FulfillmentRules
{
    /// <summary>Order statuses that block fulfillment processing entirely.</summary>
    public static bool IsBlockedForFulfillment(string orderStatus) =>
        orderStatus is "Cancelled";

    /// <summary>Picking cannot start before inventory is allocated.</summary>
    public static bool CanCreatePickList(string orderStatus) =>
        orderStatus is "Allocated" or "Picking";

    /// <summary>Items must be picked before a shipment can be packaged.</summary>
    public static bool CanPackageShipment(bool allItemsPicked) => allItemsPicked;

    /// <summary>
    /// A shipment cannot be marked dispatched without a package AND a carrier reference.
    /// </summary>
    public static bool CanDispatch(bool hasPackage, string? carrier, string? trackingNumber) =>
        hasPackage && !string.IsNullOrWhiteSpace(carrier) && !string.IsNullOrWhiteSpace(trackingNumber);

    /// <summary>Valid shipment status transitions.</summary>
    public static bool IsValidShipmentTransition(string from, string to) => (from, to) switch
    {
        ("Created", "Packed") => true,
        ("Packed", "Dispatched") => true,
        ("Dispatched", "InTransit") => true,
        ("InTransit", "Delivered") => true,
        ("Dispatched", "Delivered") => true,
        (_, "Exception") => true,
        _ => false
    };
}
