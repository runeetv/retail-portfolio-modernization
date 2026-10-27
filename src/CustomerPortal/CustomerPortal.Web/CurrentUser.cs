using System.Security.Claims;

namespace CustomerPortal.Web;

/// <summary>Strongly-typed access to the signed-in shopper's claims.</summary>
public static class CurrentUser
{
    public const string ClaimCustomerId = "CustomerId";
    public const string ClaimAccountId = "CustomerAccountId";
    public const string ClaimStatus = "CustomerStatus";

    public static int CustomerId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimCustomerId), out var v) ? v : 0;

    public static int AccountId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimAccountId), out var v) ? v : 0;

    public static string Status(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimStatus) ?? "Standard";
}
