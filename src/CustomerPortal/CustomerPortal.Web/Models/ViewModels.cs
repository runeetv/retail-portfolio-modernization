using System.ComponentModel.DataAnnotations;
using CustomerPortal.Domain;

namespace CustomerPortal.Web.Models;

public class LoginViewModel
{
    [Required] public string Username { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? Error { get; set; }
}

public class ProfileViewModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    [Phone] public string? Phone { get; set; }
    public string? ShipAddressLine { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipRegion { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipCountry { get; set; }
    public string CustomerStatus { get; set; } = "Standard";
}

public class CatalogViewModel
{
    public string? Term { get; set; }
    public int? CategoryId { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<CatalogProduct> Products { get; set; } = new();
    public Dictionary<int, int> Availability { get; set; } = new();
}

public class CartViewModel
{
    public ShoppingCart Cart { get; set; } = new();
    public decimal SubTotal { get; set; }
    public string? AppliedCoupon { get; set; }
    public string? Message { get; set; }
}

public class CheckoutViewModel
{
    public decimal SubTotal { get; set; }
    public string? AppliedCoupon { get; set; }
    public int ItemCount { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string? Error { get; set; }
}

public class OrderDetailViewModel
{
    public CustomerOrder Order { get; set; } = new();
    public bool CanCancel { get; set; }
    public bool CanReturn { get; set; }
    public List<TrackedShipment> Shipments { get; set; } = new();
}

public class SupportTicketCreateViewModel
{
    [Required] public string Subject { get; set; } = "";
    [Required] public string Body { get; set; } = "";
    public int? RelatedOrderId { get; set; }
}
