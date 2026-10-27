using FulfillmentAPI.Domain;
using FulfillmentAPI.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentAPI.Application;

/// <summary>
/// Warehouse receipt of returned merchandise (SCENARIO 5 / WORKFLOW C end).
/// Delegates to usp_ReceiveReturnAndAdjust which:
///  - only accepts 'Approved' returns (approved in OrderDesk),
///  - restocks inventory + writes WarehouseAdjustments,
///  - is idempotent (one receipt per return),
///  - does NOT change financial/refund status (receipt != refund).
/// </summary>
public class ReturnReceiptService
{
    private readonly FulfillmentDbContext _db;
    public ReturnReceiptService(FulfillmentDbContext db) => _db = db;

    public async Task<ApiResult<ReturnReceipt>> ReceiveAsync(int returnId, int warehouseId, CancellationToken ct = default)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId, ct))
            return ApiResult<ReturnReceipt>.Fail("Warehouse not found.", ApiErrorKind.NotFound);
        if (!await _db.Returns.AnyAsync(r => r.ReturnId == returnId, ct))
            return ApiResult<ReturnReceipt>.Fail("Return not found.", ApiErrorKind.NotFound);

        var conn = (SqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_ReceiveReturnAndAdjust";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@ReturnId", returnId));
        cmd.Parameters.Add(new SqlParameter("@WarehouseId", warehouseId));
        var outId = new SqlParameter("@ReturnReceiptId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(outId);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is 50031) // not approved
        {
            return ApiResult<ReturnReceipt>.Fail(ex.Message, ApiErrorKind.Conflict);
        }
        catch (SqlException ex)
        {
            return ApiResult<ReturnReceipt>.Fail(ex.Message);
        }

        var receiptId = outId.Value is int id ? id : 0;
        var receipt = await _db.ReturnReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.ReturnReceiptId == receiptId, ct);
        return receipt is null
            ? ApiResult<ReturnReceipt>.Fail("Receipt not recorded.")
            : ApiResult<ReturnReceipt>.Ok(receipt);
    }

    public async Task<List<WarehouseAdjustment>> GetAdjustmentsForReceiptAsync(int returnReceiptId, CancellationToken ct = default)
        => await _db.WarehouseAdjustments.AsNoTracking()
            .Where(a => a.RelatedReturnReceiptId == returnReceiptId).ToListAsync(ct);
}
