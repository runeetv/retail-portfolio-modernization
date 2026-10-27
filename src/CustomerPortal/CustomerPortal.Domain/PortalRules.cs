namespace CustomerPortal.Domain;

/// <summary>
/// CustomerPortal's own business rules. These are implemented in C# application
/// services (unlike OrderDesk, which uses SQL stored procedures for the
/// equivalent cancellation rule). Intentional architectural divergence.
/// </summary>
public static class PortalRules
{
    /// <summary>Return window in days (customer self-service returns).</summary>
    public const int ReturnWindowDays = 30;

    /// <summary>
    /// Customer-initiated cancellation eligibility. INTENTIONALLY STRICTER than
    /// OrderDesk's staff rule: customers may only cancel while 'Pending' or
    /// 'Confirmed'. Once 'Allocated' (warehouse began work) the customer cannot
    /// self-cancel, whereas OrderDesk staff still can (via usp_EvaluateCancellationEligibility).
    /// This is the "bounded difference in cancellation eligibility logic".
    /// </summary>
    public static bool CustomerCanCancel(string orderStatus) =>
        orderStatus is "Pending" or "Confirmed";

    /// <summary>Return allowed only for delivered orders within the return window.</summary>
    public static bool CanRequestReturn(string orderStatus, DateTime placedUtc, DateTime utcNow) =>
        orderStatus == "Delivered" && (utcNow - placedUtc).TotalDays <= ReturnWindowDays;

    /// <summary>
    /// Coupon eligibility: active, not expired, cart meets minimum, and (if the coupon
    /// targets a status) the customer's status matches. (DUPLICATED concept with
    /// OrderDesk discount gating but implemented entirely differently.)
    /// </summary>
    public static (bool ok, string? reason) EvaluateCoupon(Coupon coupon, decimal cartTotal, string customerStatus, int redemptionCount, DateTime utcNow)
    {
        if (!coupon.IsActive) return (false, "Coupon is not active.");
        if (coupon.ExpiresUtc is { } exp && exp < utcNow) return (false, "Coupon has expired.");
        if (cartTotal < coupon.MinCartTotal) return (false, $"Cart total must be at least {coupon.MinCartTotal:C}.");
        if (!string.IsNullOrWhiteSpace(coupon.RequiredStatus) &&
            !string.Equals(coupon.RequiredStatus, customerStatus, StringComparison.OrdinalIgnoreCase))
            return (false, $"Coupon requires {coupon.RequiredStatus} status.");
        if (coupon.MaxRedemptions is { } max && redemptionCount >= max) return (false, "Coupon redemption limit reached.");
        return (true, null);
    }

    /// <summary>Computes the discount amount a coupon applies to a cart total.</summary>
    public static decimal CouponDiscount(Coupon coupon, decimal cartTotal)
    {
        var raw = coupon.CouponType == "Percent"
            ? cartTotal * (coupon.Value / 100m)
            : coupon.Value;
        return Math.Round(Math.Min(raw, cartTotal), 2);
    }

    /// <summary>Valid support-ticket status transitions.</summary>
    public static bool IsValidTicketTransition(string from, string to) => (from, to) switch
    {
        ("Open", "Pending") => true,
        ("Open", "Resolved") => true,
        ("Pending", "Open") => true,
        ("Pending", "Resolved") => true,
        ("Resolved", "Closed") => true,
        ("Resolved", "Open") => true,
        _ => false
    };
}
