using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;
using OrderDesk.Infrastructure;

namespace OrderDesk.Application;

/// <summary>
/// Sales quotation workflow: create quote, add lines, request/approve discounts,
/// and convert an approved quote into an order via the SQL stored procedure
/// (transactionally safe; prevents double conversion).
/// </summary>
public class QuoteService
{
    private readonly Func<OrderDeskDbContext> _ctx;
    public QuoteService(Func<OrderDeskDbContext> contextFactory) => _ctx = contextFactory;

    public async Task<OperationResult<SalesQuote>> CreateQuoteAsync(int customerId, int salesAgentId, int validityDays = OrderDeskRules.DefaultQuoteValidityDays, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (customer is null) return OperationResult<SalesQuote>.Fail("Customer not found.");
        if (!await db.SalesAgents.AnyAsync(a => a.SalesAgentId == salesAgentId, ct))
            return OperationResult<SalesQuote>.Fail("Sales agent not found.");

        var now = DateTime.UtcNow;
        var quote = new SalesQuote
        {
            QuoteNumber = $"Q-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customerId,
            SalesAgentId = salesAgentId,
            QuoteStatus = nameof(QuoteStatus.Draft),
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(validityDays)
        };
        db.SalesQuotes.Add(quote);
        await db.SaveChangesAsync(ct);
        return OperationResult<SalesQuote>.Ok(quote);
    }

    public async Task<OperationResult> AddLineAsync(int quoteId, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return OperationResult.Fail("Quantity must be positive.");
        await using var db = _ctx();
        var quote = await db.SalesQuotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.SalesQuoteId == quoteId, ct);
        if (quote is null) return OperationResult.Fail("Quote not found.");
        if (quote.QuoteStatus is not (nameof(QuoteStatus.Draft)) && quote.QuoteStatus != nameof(QuoteStatus.PendingApproval))
            return OperationResult.Fail("Lines can only be modified on draft/pending quotes.");

        var product = await db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null) return OperationResult.Fail("Product not found.");

        var existing = quote.Lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing is not null) existing.Quantity += quantity;
        else quote.Lines.Add(new SalesQuoteLine { ProductId = productId, Quantity = quantity, UnitPrice = product.UnitPrice });

        RecalculateTotals(quote);
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>
    /// Applies a discount. If it exceeds the approval threshold, the quote moves to
    /// PendingApproval and a DiscountApproval record is created. Otherwise the quote
    /// is auto-approved.
    /// </summary>
    public async Task<OperationResult> ApplyDiscountAsync(int quoteId, decimal discountPercent, CancellationToken ct = default)
    {
        if (discountPercent is < 0 or > 100) return OperationResult.Fail("Discount must be 0-100%.");
        await using var db = _ctx();
        var quote = await db.SalesQuotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.SalesQuoteId == quoteId, ct);
        if (quote is null) return OperationResult.Fail("Quote not found.");
        if (quote.QuoteStatus == nameof(QuoteStatus.Converted)) return OperationResult.Fail("Quote already converted.");

        quote.DiscountPercent = discountPercent;
        RecalculateTotals(quote);

        if (OrderDeskRules.RequiresDiscountApproval(discountPercent))
        {
            quote.QuoteStatus = nameof(QuoteStatus.PendingApproval);
            if (!await db.DiscountApprovals.AnyAsync(d => d.SalesQuoteId == quoteId && d.ApprovalStatus == "Pending", ct))
            {
                db.DiscountApprovals.Add(new DiscountApproval
                {
                    SalesQuoteId = quoteId,
                    RequestedPercent = discountPercent,
                    ApprovalStatus = "Pending",
                    Notes = $"Discount {discountPercent}% requires approval."
                });
            }
        }
        else
        {
            quote.QuoteStatus = nameof(QuoteStatus.Approved);
        }

        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>Approve or reject a pending discount (manager action).</summary>
    public async Task<OperationResult> DecideDiscountAsync(int discountApprovalId, bool approve, int decidingAgentId, string? notes, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var ap = await db.DiscountApprovals.FirstOrDefaultAsync(d => d.DiscountApprovalId == discountApprovalId, ct);
        if (ap is null) return OperationResult.Fail("Approval request not found.");
        if (ap.ApprovalStatus != "Pending") return OperationResult.Fail("Approval already decided.");

        var quote = await db.SalesQuotes.FirstOrDefaultAsync(q => q.SalesQuoteId == ap.SalesQuoteId, ct);
        if (quote is null) return OperationResult.Fail("Quote not found.");

        ap.ApprovalStatus = approve ? "Approved" : "Rejected";
        ap.ApprovedByAgentId = decidingAgentId;
        ap.DecisionUtc = DateTime.UtcNow;
        ap.Notes = notes ?? ap.Notes;
        quote.QuoteStatus = approve ? nameof(QuoteStatus.Approved) : nameof(QuoteStatus.Rejected);

        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    /// <summary>
    /// Converts an approved quote to an order using the SQL stored procedure
    /// usp_ConvertQuoteToOrder (business rules enforced in SQL, transaction-safe,
    /// double-conversion prevented). Also pre-checks mandatory customer info in C#
    /// (DUPLICATED rule, see OrderDeskRules.CustomerCanOrder).
    /// </summary>
    public async Task<OperationResult<int>> ConvertToOrderAsync(int quoteId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var quote = await db.SalesQuotes.Include(q => q.Customer)
            .FirstOrDefaultAsync(q => q.SalesQuoteId == quoteId, ct);
        if (quote is null) return OperationResult<int>.Fail("Quote not found.");
        if (!OrderDeskRules.CanConvertToOrder(quote, DateTime.UtcNow))
            return OperationResult<int>.Fail("Quote is not in a convertible state (must be Approved, not expired, not already converted).");
        if (quote.Customer is not null && !OrderDeskRules.CustomerCanOrder(quote.Customer))
            return OperationResult<int>.Fail("Customer is missing mandatory contact/shipping information.");

        var conn = (SqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "dbo.usp_ConvertQuoteToOrder";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@SalesQuoteId", quoteId));
        var outParam = new SqlParameter("@OrderId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(outParam);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return OperationResult<int>.Ok((int)outParam.Value);
        }
        catch (SqlException ex)
        {
            return OperationResult<int>.Fail(ex.Message);
        }
    }

    public async Task<List<SalesQuote>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        await using var db = _ctx();
        var q = db.SalesQuotes.AsNoTracking().Include(x => x.Customer).Include(x => x.SalesAgent);
        IQueryable<SalesQuote> query = q;
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.QuoteStatus == status);
        return await query.OrderByDescending(x => x.CreatedUtc).Take(200).ToListAsync(ct);
    }

    public async Task<SalesQuote?> GetAsync(int quoteId, CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.SalesQuotes.AsNoTracking()
            .Include(q => q.Customer).Include(q => q.SalesAgent)
            .Include(q => q.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(q => q.SalesQuoteId == quoteId, ct);
    }

    public async Task<List<DiscountApproval>> ListPendingApprovalsAsync(CancellationToken ct = default)
    {
        await using var db = _ctx();
        return await db.DiscountApprovals.AsNoTracking().Include(d => d.SalesQuote)
            .Where(d => d.ApprovalStatus == "Pending")
            .OrderBy(d => d.DiscountApprovalId).ToListAsync(ct);
    }

    private static void RecalculateTotals(SalesQuote quote)
    {
        quote.SubTotal = quote.Lines.Sum(l => l.UnitPrice * l.Quantity);
        quote.GrandTotal = Math.Round(quote.SubTotal * (1 - quote.DiscountPercent / 100m), 2);
    }
}
