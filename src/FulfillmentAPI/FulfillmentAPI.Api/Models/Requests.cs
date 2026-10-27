using System.ComponentModel.DataAnnotations;

namespace FulfillmentAPI.Api.Models;

public class AllocateRequest
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
}

public class CreatePickListRequest
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
}

public class MarkPickedRequest
{
    [Range(0, int.MaxValue)] public int QuantityPicked { get; set; }
}

public class CreateShipmentRequest
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [Required] public string PackageCode { get; set; } = "";
    public decimal? WeightKg { get; set; }
}

public class UpdateShipmentStatusRequest
{
    [Required] public string NewStatus { get; set; } = "";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
}

public class CarrierEventRequest
{
    [Required] public string EventCode { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventUtc { get; set; }
}

public class ReceiveReturnRequest
{
    [Range(1, int.MaxValue)] public int ReturnId { get; set; }
    [Range(1, int.MaxValue)] public int WarehouseId { get; set; }
}
