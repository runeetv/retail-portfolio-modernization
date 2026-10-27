namespace FulfillmentAPI.Domain;

/* FulfillmentAPI's OWN domain model (warehouse-centric). Even for shared tables
   (Orders, Products, Inventory, Returns) it defines its own narrow shapes focused
   on logistics. Intentionally NOT shared with OrderDesk/CustomerPortal.
   Terminology note: FulfillmentAPI uses "FulfillmentOrder" for the same dbo.Orders
   row the other apps call "Order"/"CustomerOrder" (naming inconsistency on purpose). */

public class Warehouse
{
    public int WarehouseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Region { get; set; }
    public bool IsActive { get; set; }
}

public class WarehouseBin
{
    public int WarehouseBinId { get; set; }
    public int WarehouseId { get; set; }
    public string BinCode { get; set; } = "";
    public string? Aisle { get; set; }
}

/// <summary>Warehouse view of a shared dbo.Orders row (a.k.a. "Order" elsewhere).</summary>
public class FulfillmentOrder
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public string OrderStatus { get; set; } = "";
    public string OrderSource { get; set; } = "";
    public DateTime PlacedUtc { get; set; }
    public List<FulfillmentOrderLine> Lines { get; set; } = new();
}

public class FulfillmentOrderLine
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class StockRecord
{
    public int ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityAllocated { get; set; }
    public int ReorderLevel { get; set; }
    public int Available => QuantityOnHand - QuantityAllocated;
}

public class InventoryAllocation
{
    public int InventoryAllocationId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int QuantityAllocated { get; set; }
    public string AllocationStatus { get; set; } = "Allocated";
    public DateTime CreatedUtc { get; set; }
}

public class PickList
{
    public int PickListId { get; set; }
    public int OrderId { get; set; }
    public int WarehouseId { get; set; }
    public string PickStatus { get; set; } = "Created";
    public DateTime CreatedUtc { get; set; }
    public List<PickListItem> Items { get; set; } = new();
}

public class PickListItem
{
    public int PickListItemId { get; set; }
    public int PickListId { get; set; }
    public int ProductId { get; set; }
    public int? WarehouseBinId { get; set; }
    public int QuantityToPick { get; set; }
    public int QuantityPicked { get; set; }
    public bool IsPicked { get; set; }
}

public class Shipment
{
    public int ShipmentId { get; set; }
    public int OrderId { get; set; }
    public string ShipmentStatus { get; set; } = "Created";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? DispatchedUtc { get; set; }
    public List<ShipmentPackage> Packages { get; set; } = new();
    public List<CarrierEvent> Events { get; set; } = new();
}

public class ShipmentPackage
{
    public int ShipmentPackageId { get; set; }
    public int ShipmentId { get; set; }
    public string PackageCode { get; set; } = "";
    public decimal? WeightKg { get; set; }
}

public class CarrierEvent
{
    public int CarrierEventId { get; set; }
    public int ShipmentId { get; set; }
    public string EventCode { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventUtc { get; set; }
    public DateTime RecordedUtc { get; set; }
}

/// <summary>Warehouse view of a shared dbo.Returns row.</summary>
public class ReturnForReceipt
{
    public int ReturnId { get; set; }
    public string ReturnNumber { get; set; } = "";
    public int OrderId { get; set; }
    public string ReturnStatus { get; set; } = "";
}

public class ReturnReceipt
{
    public int ReturnReceiptId { get; set; }
    public int ReturnId { get; set; }
    public int WarehouseId { get; set; }
    public string ReceivedStatus { get; set; } = "Received";
    public DateTime ReceivedUtc { get; set; }
}

public class WarehouseAdjustment
{
    public int WarehouseAdjustmentId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int QuantityDelta { get; set; }
    public string Reason { get; set; } = "";
    public int? RelatedReturnReceiptId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
