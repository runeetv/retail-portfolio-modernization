using FulfillmentAPI.Domain;
using FulfillmentAPI.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Application;

/// <summary>
/// Inventory reservation/allocation (SCENARIO 4, FulfillmentAPI side - physical
/// warehouse allocation). Delegates to the SQL stored procedure usp_AllocateInventory
/// which enforces "cannot allocate beyond available" and blocks cancelled orders,
/// and is idempotent for already-allocated products.
/// </summary>
public class AllocationService
{
    private readonly FulfillmentDbContext _db;
    public AllocationService(FulfillmentDbContext db) => _db = db;

    public async Task<ApiResult<AllocationResult>> AllocateAsync(int orderId, int warehouseId, CancellationToken ct = default)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
            return ApiResult<AllocationResult>.Fail("Warehouse not found.", ApiErrorKind.NotFound);
        if (!await _db.Orders.AnyAsync(o => o.OrderId == orderId, ct))
            return ApiResult<AllocationResult>.Fail("Order not found.", ApiErrorKind.NotFound);

        var conn = (SqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_AllocateInventory";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@OrderId", orderId));
        cmd.Parameters.Add(new SqlParameter("@WarehouseId", warehouseId));
        var outCount = new SqlParameter("@AllocatedCount", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(outCount);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return ApiResult<AllocationResult>.Ok(new AllocationResult(orderId, warehouseId, (int)outCount.Value));
        }
        catch (SqlException ex) when (ex.Number is 50021) // cancelled order
        {
            return ApiResult<AllocationResult>.Fail(ex.Message, ApiErrorKind.Conflict);
        }
        catch (SqlException ex) when (ex.Number is 50022) // insufficient stock
        {
            return ApiResult<AllocationResult>.Fail(ex.Message, ApiErrorKind.Conflict);
        }
        catch (SqlException ex)
        {
            return ApiResult<AllocationResult>.Fail(ex.Message);
        }
    }

    public async Task<List<InventoryAllocation>> GetAllocationsAsync(int orderId, CancellationToken ct = default)
        => await _db.Allocations.AsNoTracking().Where(a => a.OrderId == orderId).ToListAsync(ct);
}

public sealed record AllocationResult(int OrderId, int WarehouseId, int ProductsAllocated);
