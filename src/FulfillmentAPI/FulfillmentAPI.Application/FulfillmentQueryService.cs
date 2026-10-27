using FulfillmentAPI.Domain;
using FulfillmentAPI.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Application;

/// <summary>Read-only queries: pending fulfillment orders, availability, tracking, summary.</summary>
public class FulfillmentQueryService
{
    private readonly FulfillmentDbContext _db;
    public FulfillmentQueryService(FulfillmentDbContext db) => _db = db;

    /// <summary>
    /// Orders eligible for fulfillment: Confirmed or Allocated and not cancelled.
    /// Works for orders from BOTH origins (OrderDesk + CustomerPortal).
    /// </summary>
    public async Task<List<FulfillmentOrder>> GetPendingOrdersAsync(CancellationToken ct = default)
        => await _db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.OrderStatus == "Confirmed" || o.OrderStatus == "Allocated")
            .OrderBy(o => o.PlacedUtc).Take(200).ToListAsync(ct);

    public async Task<FulfillmentOrder?> GetOrderAsync(int orderId, CancellationToken ct = default)
        => await _db.Orders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

    public async Task<StockRecord?> GetStockAsync(int productId, CancellationToken ct = default)
        => await _db.Inventory.AsNoTracking().FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    public async Task<List<Warehouse>> GetWarehousesAsync(CancellationToken ct = default)
        => await _db.Warehouses.AsNoTracking().Where(w => w.IsActive).ToListAsync(ct);

    public async Task<Shipment?> GetShipmentTrackingAsync(int shipmentId, CancellationToken ct = default)
        => await _db.Shipments.AsNoTracking().Include(s => s.Events).Include(s => s.Packages)
            .FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);

    public async Task<List<Shipment>> GetShipmentsForOrderAsync(int orderId, CancellationToken ct = default)
        => await _db.Shipments.AsNoTracking().Include(s => s.Events)
            .Where(s => s.OrderId == orderId).ToListAsync(ct);

    /// <summary>
    /// Warehouse activity summary via the cross-domain stored procedure
    /// usp_WarehouseActivitySummary (joins fulfillment + shared + OrderDesk tables).
    /// </summary>
    public async Task<List<WarehouseActivityRow>> GetActivitySummaryAsync(int? warehouseId = null, CancellationToken ct = default)
    {
        var conn = (SqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_WarehouseActivitySummary";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@WarehouseId", (object?)warehouseId ?? DBNull.Value));
        var rows = new List<WarehouseActivityRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new WarehouseActivityRow
            {
                WarehouseId = reader.GetInt32(0),
                WarehouseName = reader.GetString(1),
                PickLists = reader.GetInt32(2),
                Shipments = reader.GetInt32(3),
                Allocations = reader.GetInt32(4),
                OrdersTouched = reader.GetInt32(5),
                StaffOrders = reader.GetInt32(6),
                WebOrders = reader.GetInt32(7),
                DistinctCustomers = reader.GetInt32(8),
                DistinctAgents = reader.GetInt32(9)
            });
        }
        return rows;
    }
}

public class WarehouseActivityRow
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public int PickLists { get; set; }
    public int Shipments { get; set; }
    public int Allocations { get; set; }
    public int OrdersTouched { get; set; }
    public int StaffOrders { get; set; }
    public int WebOrders { get; set; }
    public int DistinctCustomers { get; set; }
    public int DistinctAgents { get; set; }
}
