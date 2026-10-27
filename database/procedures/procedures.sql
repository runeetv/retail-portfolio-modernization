/* ============================================================================
   LegacyRetailDB - Stored procedures
   Business rules enforced in SQL (intentional: not in C# services).
   ============================================================================ */
SET NOCOUNT ON;
GO

/* ----------------------------------------------------------------------------
   usp_ConvertQuoteToOrder  (OrderDesk)
   Transactionally converts an APPROVED, non-expired SalesQuote into an Order.
   Prevents double conversion via ConvertedOrderId uniqueness + status guard.
   Also computes and records the sales commission.
   RULES ENFORCED HERE (SQL, not C#):
     - Only 'Approved' quotes may convert.
     - Expired quotes (ExpiresUtc < now) may not convert.
     - A quote may convert at most once.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_ConvertQuoteToOrder','P') IS NOT NULL DROP PROCEDURE dbo.usp_ConvertQuoteToOrder;
GO
CREATE PROCEDURE dbo.usp_ConvertQuoteToOrder
    @SalesQuoteId INT,
    @OrderId      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CustomerId INT, @AgentId INT, @Status NVARCHAR(20),
            @Expires DATETIME2(0), @Disc DECIMAL(5,2), @Converted INT,
            @SubTotal DECIMAL(18,2), @GrandTotal DECIMAL(18,2),
            @Rate DECIMAL(5,4), @OrderNumber NVARCHAR(30);

    BEGIN TRAN;

    SELECT @CustomerId = q.CustomerId, @AgentId = q.SalesAgentId, @Status = q.QuoteStatus,
           @Expires = q.ExpiresUtc, @Disc = q.DiscountPercent, @Converted = q.ConvertedOrderId,
           @SubTotal = q.SubTotal, @GrandTotal = q.GrandTotal
    FROM dbo.SalesQuotes q WITH (UPDLOCK, HOLDLOCK)
    WHERE q.SalesQuoteId = @SalesQuoteId;

    IF @CustomerId IS NULL
    BEGIN
        ROLLBACK TRAN; THROW 50010, 'Quote not found.', 1;
    END
    IF @Converted IS NOT NULL
    BEGIN
        ROLLBACK TRAN; THROW 50011, 'Quote already converted.', 1;
    END
    IF @Status <> 'Approved'
    BEGIN
        ROLLBACK TRAN; THROW 50012, 'Only approved quotes may convert to orders.', 1;
    END
    IF @Expires < SYSUTCDATETIME()
    BEGIN
        ROLLBACK TRAN; THROW 50013, 'Quote has expired and cannot convert.', 1;
    END

    /* Mandatory customer information guard (duplicated rule - also in C#). */
    IF EXISTS (SELECT 1 FROM dbo.Customers c
               WHERE c.CustomerId = @CustomerId
                 AND (c.Email IS NULL OR LTRIM(RTRIM(c.Email)) = ''
                   OR c.ShipAddressLine IS NULL OR LTRIM(RTRIM(c.ShipAddressLine)) = ''))
    BEGIN
        ROLLBACK TRAN; THROW 50014, 'Customer is missing mandatory contact/shipping information.', 1;
    END

    SET @OrderNumber = 'ORD-' + RIGHT('00000000' + CAST(ABS(CHECKSUM(NEWID())) % 100000000 AS NVARCHAR(8)), 8);

    INSERT dbo.Orders (OrderNumber, CustomerId, OrderStatus, OrderSource, SubTotal, DiscountTotal, GrandTotal, PlacedByAgentId, SourceQuoteId)
    VALUES (@OrderNumber, @CustomerId, 'Confirmed', 'OrderDesk', @SubTotal, @SubTotal - @GrandTotal, @GrandTotal, @AgentId, @SalesQuoteId);

    SET @OrderId = SCOPE_IDENTITY();

    INSERT dbo.OrderLines (OrderId, ProductId, Quantity, UnitPrice, LineDiscount)
    SELECT @OrderId, l.ProductId, l.Quantity, l.UnitPrice,
           CAST(l.UnitPrice * l.Quantity * (@Disc/100.0) AS DECIMAL(18,2))
    FROM dbo.SalesQuoteLines l WHERE l.SalesQuoteId = @SalesQuoteId;

    UPDATE dbo.SalesQuotes
       SET QuoteStatus = 'Converted', ConvertedOrderId = @OrderId
     WHERE SalesQuoteId = @SalesQuoteId;

    /* Commission = order grand total * agent rate, boosted when discount is high. */
    SELECT @Rate = CommissionRate FROM dbo.SalesAgents WHERE SalesAgentId = @AgentId;
    INSERT dbo.SalesCommissions (SalesAgentId, OrderId, OrderValue, DiscountPercent, CommissionAmount)
    VALUES (@AgentId, @OrderId, @GrandTotal, @Disc,
            CAST(@GrandTotal * @Rate * (CASE WHEN @Disc >= 20 THEN 0.5 ELSE 1.0 END) AS DECIMAL(18,2)));

    COMMIT TRAN;
END
GO

/* ----------------------------------------------------------------------------
   usp_EvaluateCancellationEligibility  (OrderDesk)
   RULE ENFORCED IN SQL: OrderDesk staff cancellation eligibility.
   OrderDesk allows cancellation for Pending/Confirmed/Allocated (more permissive).
   Returns IsEligible bit + Reason. (CustomerPortal enforces its OWN, stricter
   rule in a C# application service - intentional divergence.)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_EvaluateCancellationEligibility','P') IS NOT NULL DROP PROCEDURE dbo.usp_EvaluateCancellationEligibility;
GO
CREATE PROCEDURE dbo.usp_EvaluateCancellationEligibility
    @OrderId INT,
    @IsEligible BIT OUTPUT,
    @Reason NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Status NVARCHAR(20);
    SELECT @Status = OrderStatus FROM dbo.Orders WHERE OrderId = @OrderId;

    SET @IsEligible = 0;
    IF @Status IS NULL
        SET @Reason = 'Order not found.';
    ELSE IF @Status = 'Cancelled'
        SET @Reason = 'Order already cancelled.';
    ELSE IF @Status IN ('Pending','Confirmed','Allocated')
    BEGIN
        SET @IsEligible = 1; SET @Reason = 'Eligible for staff cancellation.';
    END
    ELSE
        SET @Reason = 'Order has progressed to ' + @Status + '; requires override authorization.';
END
GO

/* ----------------------------------------------------------------------------
   usp_AllocateInventory  (FulfillmentAPI)
   RULE ENFORCED IN SQL: cannot allocate beyond available (OnHand - Allocated).
   Blocks allocation for cancelled orders. Updates Inventory.QuantityAllocated.
   Idempotency: skips products already fully allocated for the order.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_AllocateInventory','P') IS NOT NULL DROP PROCEDURE dbo.usp_AllocateInventory;
GO
CREATE PROCEDURE dbo.usp_AllocateInventory
    @OrderId     INT,
    @WarehouseId INT,
    @AllocatedCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Status NVARCHAR(20);
    SET @AllocatedCount = 0;

    BEGIN TRAN;

    SELECT @Status = OrderStatus FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderId = @OrderId;
    IF @Status IS NULL
    BEGIN
        ROLLBACK TRAN; THROW 50020, 'Order not found.', 1;
    END
    IF @Status = 'Cancelled'
    BEGIN
        ROLLBACK TRAN; THROW 50021, 'Cannot allocate inventory for a cancelled order.', 1;
    END

    DECLARE @ProductId INT, @Need INT, @Avail INT;
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT ol.ProductId, SUM(ol.Quantity) AS Need
        FROM dbo.OrderLines ol
        WHERE ol.OrderId = @OrderId
          AND NOT EXISTS (SELECT 1 FROM dbo.InventoryAllocations ia
                          WHERE ia.OrderId = @OrderId AND ia.ProductId = ol.ProductId
                            AND ia.AllocationStatus = 'Allocated')
        GROUP BY ol.ProductId;
    OPEN c; FETCH NEXT FROM c INTO @ProductId, @Need;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @Avail = (QuantityOnHand - QuantityAllocated)
        FROM dbo.Inventory WITH (UPDLOCK, HOLDLOCK) WHERE ProductId = @ProductId;

        IF @Avail IS NULL OR @Avail < @Need
        BEGIN
            CLOSE c; DEALLOCATE c; ROLLBACK TRAN;
            THROW 50022, 'Insufficient available inventory to allocate.', 1;
        END

        UPDATE dbo.Inventory SET QuantityAllocated = QuantityAllocated + @Need WHERE ProductId = @ProductId;
        INSERT dbo.InventoryAllocations (OrderId, ProductId, WarehouseId, QuantityAllocated)
        VALUES (@OrderId, @ProductId, @WarehouseId, @Need);
        SET @AllocatedCount = @AllocatedCount + 1;

        FETCH NEXT FROM c INTO @ProductId, @Need;
    END
    CLOSE c; DEALLOCATE c;

    IF @Status IN ('Pending','Confirmed')
        UPDATE dbo.Orders SET OrderStatus = 'Allocated' WHERE OrderId = @OrderId;

    COMMIT TRAN;
END
GO

/* ----------------------------------------------------------------------------
   usp_ReceiveReturnAndAdjust  (FulfillmentAPI)
   RULE ENFORCED IN SQL: warehouse receipt restocks inventory but does NOT
   change financial/refund status. Idempotent on ReturnReceipts.ReturnId.
   Only returns in 'Approved' state may be received.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_ReceiveReturnAndAdjust','P') IS NOT NULL DROP PROCEDURE dbo.usp_ReceiveReturnAndAdjust;
GO
CREATE PROCEDURE dbo.usp_ReceiveReturnAndAdjust
    @ReturnId    INT,
    @WarehouseId INT,
    @ReturnReceiptId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Status NVARCHAR(20);
    SET @ReturnReceiptId = NULL;

    BEGIN TRAN;

    /* Idempotency: already received -> return existing receipt id. */
    SELECT @ReturnReceiptId = ReturnReceiptId FROM dbo.ReturnReceipts WHERE ReturnId = @ReturnId;
    IF @ReturnReceiptId IS NOT NULL
    BEGIN
        COMMIT TRAN; RETURN;
    END

    SELECT @Status = ReturnStatus FROM dbo.Returns WITH (UPDLOCK, HOLDLOCK) WHERE ReturnId = @ReturnId;
    IF @Status IS NULL
    BEGIN
        ROLLBACK TRAN; THROW 50030, 'Return not found.', 1;
    END
    IF @Status <> 'Approved'
    BEGIN
        ROLLBACK TRAN; THROW 50031, 'Only approved returns may be received at the warehouse.', 1;
    END

    INSERT dbo.ReturnReceipts (ReturnId, WarehouseId, ReceivedStatus)
    VALUES (@ReturnId, @WarehouseId, 'Restocked');
    SET @ReturnReceiptId = SCOPE_IDENTITY();

    /* Restock inventory from return lines. */
    UPDATE inv SET inv.QuantityOnHand = inv.QuantityOnHand + rl.Qty
    FROM dbo.Inventory inv
    JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.ReturnLines WHERE ReturnId = @ReturnId GROUP BY ProductId) rl
      ON rl.ProductId = inv.ProductId;

    INSERT dbo.WarehouseAdjustments (ProductId, WarehouseId, QuantityDelta, Reason, RelatedReturnReceiptId)
    SELECT rl.ProductId, @WarehouseId, SUM(rl.Quantity), 'Return restock', @ReturnReceiptId
    FROM dbo.ReturnLines rl WHERE rl.ReturnId = @ReturnId GROUP BY rl.ProductId;

    /* Warehouse receipt marks the return 'Received' (NOT refunded). */
    UPDATE dbo.Returns SET ReturnStatus = 'Received' WHERE ReturnId = @ReturnId;

    COMMIT TRAN;
END
GO

/* ----------------------------------------------------------------------------
   usp_WarehouseActivitySummary  (FulfillmentAPI)
   Complex cross-domain reporting query joining Orders (shared), Shipments,
   PickLists, InventoryAllocations (fulfillment), Customers (shared) and
   SalesAgents (OrderDesk) - spans three business domains intentionally.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_WarehouseActivitySummary','P') IS NOT NULL DROP PROCEDURE dbo.usp_WarehouseActivitySummary;
GO
CREATE PROCEDURE dbo.usp_WarehouseActivitySummary
    @WarehouseId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        w.WarehouseId,
        w.Name                              AS WarehouseName,
        COUNT(DISTINCT pl.PickListId)       AS PickLists,
        COUNT(DISTINCT s.ShipmentId)        AS Shipments,
        COUNT(DISTINCT ia.InventoryAllocationId) AS Allocations,
        COUNT(DISTINCT o.OrderId)           AS OrdersTouched,
        COUNT(DISTINCT CASE WHEN o.OrderSource = 'OrderDesk' THEN o.OrderId END)      AS StaffOrders,
        COUNT(DISTINCT CASE WHEN o.OrderSource = 'CustomerPortal' THEN o.OrderId END) AS WebOrders,
        COUNT(DISTINCT c.CustomerId)        AS DistinctCustomers,
        COUNT(DISTINCT sa.SalesAgentId)     AS DistinctAgents
    FROM dbo.Warehouses w
    LEFT JOIN dbo.PickLists pl           ON pl.WarehouseId = w.WarehouseId
    LEFT JOIN dbo.InventoryAllocations ia ON ia.WarehouseId = w.WarehouseId
    LEFT JOIN dbo.Orders o               ON o.OrderId IN (pl.OrderId, ia.OrderId)
    LEFT JOIN dbo.Shipments s            ON s.OrderId = o.OrderId
    LEFT JOIN dbo.Customers c            ON c.CustomerId = o.CustomerId
    LEFT JOIN dbo.SalesAgents sa         ON sa.SalesAgentId = o.PlacedByAgentId
    WHERE (@WarehouseId IS NULL OR w.WarehouseId = @WarehouseId)
    GROUP BY w.WarehouseId, w.Name
    ORDER BY w.WarehouseId;
END
GO
