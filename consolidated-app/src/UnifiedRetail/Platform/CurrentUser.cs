using System.Security.Claims;

namespace UnifiedRetail.Platform;

/// <summary>
/// Strongly-typed access to the signed-in principal's claims. Customer claims mirror the
/// original CustomerPortal.Web.CurrentUser (CP-RULE-014 record ownership depends on these).
/// Staff/warehouse principals additionally carry an AgentId claim where relevant.
/// </summary>
public static class CurrentUser
{
    public const string ClaimCustomerId = "CustomerId";
    public const string ClaimAccountId = "CustomerAccountId";
    public const string ClaimStatus = "CustomerStatus";
    public const string ClaimAgentId = "SalesAgentId";
    public const string ClaimWarehouseId = "WarehouseId";

    public static int CustomerId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimCustomerId), out var v) ? v : 0;

    public static int AccountId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimAccountId), out var v) ? v : 0;

    public static string Status(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimStatus) ?? "Standard";

    public static int AgentId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimAgentId), out var v) ? v : 0;
}
