namespace OrderDesk.Domain;

/// <summary>
/// Pure, testable OrderDesk business rules. These are OrderDesk's own rules and
/// are intentionally separate from CustomerPortal's (even where they overlap).
/// </summary>
public static class OrderDeskRules
{
    /// <summary>Discounts above this percent require an explicit DiscountApproval.</summary>
    public const decimal DiscountApprovalThresholdPercent = 15m;

    /// <summary>Default number of days before a new quote expires.</summary>
    public const int DefaultQuoteValidityDays = 14;

    /// <summary>
    /// OrderDesk staff cancellation eligibility (DUPLICATED concept with CustomerPortal,
    /// but intentionally MORE permissive — allows 'Allocated' too). The authoritative
    /// staff rule also exists as SQL usp_EvaluateCancellationEligibility; this C#
    /// version mirrors it for pre-checks and unit tests.
    /// </summary>
    public static bool IsStaffCancellable(string orderStatus) =>
        orderStatus is "Pending" or "Confirmed" or "Allocated";

    /// <summary>
    /// A quote may convert only when Approved and not expired. (Also enforced in SQL.)
    /// </summary>
    public static bool CanConvertToOrder(SalesQuote quote, DateTime utcNow) =>
        quote.ConvertedOrderId is null
        && string.Equals(quote.QuoteStatus, nameof(QuoteStatus.Approved), StringComparison.Ordinal)
        && !quote.IsExpired(utcNow);

    public static bool RequiresDiscountApproval(decimal discountPercent) =>
        discountPercent > DiscountApprovalThresholdPercent;

    /// <summary>
    /// Commission = order value * agent rate, halved when the applied discount is
    /// aggressive (>= 20%). Matches the SQL commission formula in usp_ConvertQuoteToOrder.
    /// </summary>
    public static decimal CalculateCommission(decimal orderValue, decimal agentRate, decimal discountPercent)
    {
        var modifier = discountPercent >= 20m ? 0.5m : 1.0m;
        return Math.Round(orderValue * agentRate * modifier, 2);
    }

    /// <summary>Mandatory customer info guard (duplicated with SQL + CustomerPortal).</summary>
    public static bool CustomerCanOrder(Customer c) => c.HasMandatoryOrderInfo;
}
