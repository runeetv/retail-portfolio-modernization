using FulfillmentAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Infrastructure;

/// <summary>
/// FulfillmentAPI's OWN DbContext over the shared LegacyRetailDB. Warehouse-focused
/// mappings; does not own schema (DbProvisioner does).
/// </summary>
public class FulfillmentDbContext : DbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options) { }

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseBin> WarehouseBins => Set<WarehouseBin>();
    public DbSet<FulfillmentOrder> Orders => Set<FulfillmentOrder>();
    public DbSet<FulfillmentOrderLine> OrderLines => Set<FulfillmentOrderLine>();
    public DbSet<StockRecord> Inventory => Set<StockRecord>();
    public DbSet<InventoryAllocation> Allocations => Set<InventoryAllocation>();
    public DbSet<PickList> PickLists => Set<PickList>();
    public DbSet<PickListItem> PickListItems => Set<PickListItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentPackage> ShipmentPackages => Set<ShipmentPackage>();
    public DbSet<CarrierEvent> CarrierEvents => Set<CarrierEvent>();
    public DbSet<ReturnForReceipt> Returns => Set<ReturnForReceipt>();
    public DbSet<ReturnReceipt> ReturnReceipts => Set<ReturnReceipt>();
    public DbSet<WarehouseAdjustment> WarehouseAdjustments => Set<WarehouseAdjustment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Warehouse>(e => { e.ToTable("Warehouses"); e.HasKey(x => x.WarehouseId); });
        b.Entity<WarehouseBin>(e => { e.ToTable("WarehouseBins"); e.HasKey(x => x.WarehouseBinId); });

        b.Entity<FulfillmentOrder>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.OrderId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
        });
        b.Entity<FulfillmentOrderLine>(e => { e.ToTable("OrderLines"); e.HasKey(x => x.OrderLineId); });

        b.Entity<StockRecord>(e =>
        {
            e.ToTable("Inventory");
            e.HasKey(x => x.ProductId);
            e.Ignore(x => x.Available);
        });

        b.Entity<InventoryAllocation>(e => { e.ToTable("InventoryAllocations"); e.HasKey(x => x.InventoryAllocationId); });

        b.Entity<PickList>(e =>
        {
            e.ToTable("PickLists");
            e.HasKey(x => x.PickListId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PickListId);
        });
        b.Entity<PickListItem>(e => { e.ToTable("PickListItems"); e.HasKey(x => x.PickListItemId); });

        b.Entity<Shipment>(e =>
        {
            e.ToTable("Shipments");
            e.HasKey(x => x.ShipmentId);
            e.HasMany(x => x.Packages).WithOne().HasForeignKey(x => x.ShipmentId);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ShipmentId);
        });
        b.Entity<ShipmentPackage>(e => { e.ToTable("ShipmentPackages"); e.HasKey(x => x.ShipmentPackageId); });
        b.Entity<CarrierEvent>(e => { e.ToTable("CarrierEvents"); e.HasKey(x => x.CarrierEventId); });

        b.Entity<ReturnForReceipt>(e => { e.ToTable("Returns"); e.HasKey(x => x.ReturnId); });
        b.Entity<ReturnReceipt>(e => { e.ToTable("ReturnReceipts"); e.HasKey(x => x.ReturnReceiptId); });
        b.Entity<WarehouseAdjustment>(e => { e.ToTable("WarehouseAdjustments"); e.HasKey(x => x.WarehouseAdjustmentId); });
    }
}
