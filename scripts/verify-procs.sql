SET NOCOUNT ON;
/* Pick an Approved, non-expired quote and convert it. */
DECLARE @q INT = (SELECT TOP 1 SalesQuoteId FROM dbo.SalesQuotes
                  WHERE QuoteStatus='Approved' AND ExpiresUtc > SYSUTCDATETIME() AND ConvertedOrderId IS NULL
                  ORDER BY SalesQuoteId);
PRINT 'Converting quote: ' + ISNULL(CAST(@q AS NVARCHAR(10)),'(none available)');
IF @q IS NOT NULL
BEGIN
    DECLARE @oid INT;
    EXEC dbo.usp_ConvertQuoteToOrder @SalesQuoteId=@q, @OrderId=@oid OUTPUT;
    PRINT 'Created OrderId: ' + CAST(@oid AS NVARCHAR(10));
    SELECT OrderId, OrderNumber, OrderStatus, OrderSource, GrandTotal, SourceQuoteId FROM dbo.Orders WHERE OrderId=@oid;
    SELECT SalesCommissionId, CommissionAmount FROM dbo.SalesCommissions WHERE OrderId=@oid;
END

/* Cancellation eligibility eval */
DECLARE @anyOrder INT = (SELECT TOP 1 OrderId FROM dbo.Orders WHERE OrderStatus='Confirmed' ORDER BY OrderId);
DECLARE @elig BIT, @reason NVARCHAR(200);
EXEC dbo.usp_EvaluateCancellationEligibility @OrderId=@anyOrder, @IsEligible=@elig OUTPUT, @Reason=@reason OUTPUT;
PRINT 'Cancellation eligibility for order ' + CAST(@anyOrder AS NVARCHAR(10)) + ': ' + CAST(@elig AS NVARCHAR(2)) + ' - ' + @reason;

/* Warehouse summary (cross-domain join) */
EXEC dbo.usp_WarehouseActivitySummary;
