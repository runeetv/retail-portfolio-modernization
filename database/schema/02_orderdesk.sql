/* ============================================================================
   LegacyRetailDB - OrderDesk-owned tables
   ============================================================================ */
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.SalesAgents','U') IS NULL
CREATE TABLE dbo.SalesAgents
(
    SalesAgentId    INT IDENTITY(1,1) CONSTRAINT PK_SalesAgents PRIMARY KEY,
    AgentCode       NVARCHAR(20)  NOT NULL,
    FullName        NVARCHAR(160) NOT NULL,
    Email           NVARCHAR(200) NOT NULL,
    CommissionRate  DECIMAL(5,4)  NOT NULL CONSTRAINT DF_SalesAgents_Rate DEFAULT(0.0200),
    CanOverrideCancellation BIT   NOT NULL CONSTRAINT DF_SalesAgents_Override DEFAULT(0),
    IsActive        BIT           NOT NULL CONSTRAINT DF_SalesAgents_Active DEFAULT(1),
    CONSTRAINT UQ_SalesAgents_Code UNIQUE (AgentCode),
    CONSTRAINT CK_SalesAgents_Rate CHECK (CommissionRate >= 0 AND CommissionRate <= 1)
);
GO

/* A SalesQuote can be converted into exactly one Order (prevented double-convert
   by the usp_ConvertQuoteToOrder stored procedure + ConvertedOrderId uniqueness). */
IF OBJECT_ID('dbo.SalesQuotes','U') IS NULL
CREATE TABLE dbo.SalesQuotes
(
    SalesQuoteId    INT IDENTITY(1,1) CONSTRAINT PK_SalesQuotes PRIMARY KEY,
    QuoteNumber     NVARCHAR(30)  NOT NULL,
    CustomerId      INT           NOT NULL,
    SalesAgentId    INT           NOT NULL,
    QuoteStatus     NVARCHAR(20)  NOT NULL CONSTRAINT DF_SalesQuotes_Status DEFAULT('Draft'),
    DiscountPercent DECIMAL(5,2)  NOT NULL CONSTRAINT DF_SalesQuotes_Disc DEFAULT(0),
    SubTotal        DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesQuotes_SubTotal DEFAULT(0),
    GrandTotal      DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesQuotes_GrandTotal DEFAULT(0),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SalesQuotes_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    ExpiresUtc      DATETIME2(0)  NOT NULL,
    ConvertedOrderId INT          NULL,
    CONSTRAINT UQ_SalesQuotes_Number UNIQUE (QuoteNumber),
    CONSTRAINT FK_SalesQuotes_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT FK_SalesQuotes_Agent FOREIGN KEY (SalesAgentId) REFERENCES dbo.SalesAgents(SalesAgentId),
    CONSTRAINT FK_SalesQuotes_Order FOREIGN KEY (ConvertedOrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT CK_SalesQuotes_Status CHECK (QuoteStatus IN ('Draft','PendingApproval','Approved','Rejected','Expired','Converted')),
    CONSTRAINT CK_SalesQuotes_Disc CHECK (DiscountPercent >= 0 AND DiscountPercent <= 100)
);
GO
/* Filtered unique index: a quote converts to at most one order (multiple NULLs allowed). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_SalesQuotes_ConvertedOrder' AND object_id=OBJECT_ID('dbo.SalesQuotes'))
CREATE UNIQUE INDEX UQ_SalesQuotes_ConvertedOrder ON dbo.SalesQuotes(ConvertedOrderId) WHERE ConvertedOrderId IS NOT NULL;
GO

IF OBJECT_ID('dbo.SalesQuoteLines','U') IS NULL
CREATE TABLE dbo.SalesQuoteLines
(
    SalesQuoteLineId INT IDENTITY(1,1) CONSTRAINT PK_SalesQuoteLines PRIMARY KEY,
    SalesQuoteId    INT           NOT NULL,
    ProductId       INT           NOT NULL,
    Quantity        INT           NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_SalesQuoteLines_Quote FOREIGN KEY (SalesQuoteId) REFERENCES dbo.SalesQuotes(SalesQuoteId),
    CONSTRAINT FK_SalesQuoteLines_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_SalesQuoteLines_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_SalesQuoteLines_Price CHECK (UnitPrice >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SalesQuoteLines_Quote' AND object_id=OBJECT_ID('dbo.SalesQuoteLines'))
CREATE INDEX IX_SalesQuoteLines_Quote ON dbo.SalesQuoteLines(SalesQuoteId);
GO

IF OBJECT_ID('dbo.DiscountApprovals','U') IS NULL
CREATE TABLE dbo.DiscountApprovals
(
    DiscountApprovalId INT IDENTITY(1,1) CONSTRAINT PK_DiscountApprovals PRIMARY KEY,
    SalesQuoteId    INT           NOT NULL,
    RequestedPercent DECIMAL(5,2) NOT NULL,
    ApprovalStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_DiscountApprovals_Status DEFAULT('Pending'),
    ApprovedByAgentId INT         NULL,
    DecisionUtc     DATETIME2(0)  NULL,
    Notes           NVARCHAR(400) NULL,
    CONSTRAINT FK_DiscountApprovals_Quote FOREIGN KEY (SalesQuoteId) REFERENCES dbo.SalesQuotes(SalesQuoteId),
    CONSTRAINT FK_DiscountApprovals_Agent FOREIGN KEY (ApprovedByAgentId) REFERENCES dbo.SalesAgents(SalesAgentId),
    CONSTRAINT CK_DiscountApprovals_Status CHECK (ApprovalStatus IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_DiscountApprovals_Percent CHECK (RequestedPercent >= 0 AND RequestedPercent <= 100)
);
GO

IF OBJECT_ID('dbo.SalesCommissions','U') IS NULL
CREATE TABLE dbo.SalesCommissions
(
    SalesCommissionId INT IDENTITY(1,1) CONSTRAINT PK_SalesCommissions PRIMARY KEY,
    SalesAgentId    INT           NOT NULL,
    OrderId         INT           NOT NULL,
    OrderValue      DECIMAL(18,2) NOT NULL,
    DiscountPercent DECIMAL(5,2)  NOT NULL,
    CommissionAmount DECIMAL(18,2) NOT NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SalesCommissions_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_SalesCommissions_Order UNIQUE (OrderId),
    CONSTRAINT FK_SalesCommissions_Agent FOREIGN KEY (SalesAgentId) REFERENCES dbo.SalesAgents(SalesAgentId),
    CONSTRAINT FK_SalesCommissions_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT CK_SalesCommissions_Amount CHECK (CommissionAmount >= 0)
);
GO

IF OBJECT_ID('dbo.SalesAgentTasks','U') IS NULL
CREATE TABLE dbo.SalesAgentTasks
(
    SalesAgentTaskId INT IDENTITY(1,1) CONSTRAINT PK_SalesAgentTasks PRIMARY KEY,
    SalesAgentId    INT           NOT NULL,
    Title           NVARCHAR(160) NOT NULL,
    Details         NVARCHAR(600) NULL,
    RelatedCustomerId INT         NULL,
    RelatedQuoteId  INT           NULL,
    TaskStatus      NVARCHAR(20)  NOT NULL CONSTRAINT DF_SalesAgentTasks_Status DEFAULT('Open'),
    DueUtc          DATETIME2(0)  NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SalesAgentTasks_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_SalesAgentTasks_Agent FOREIGN KEY (SalesAgentId) REFERENCES dbo.SalesAgents(SalesAgentId),
    CONSTRAINT FK_SalesAgentTasks_Customer FOREIGN KEY (RelatedCustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT FK_SalesAgentTasks_Quote FOREIGN KEY (RelatedQuoteId) REFERENCES dbo.SalesQuotes(SalesQuoteId),
    CONSTRAINT CK_SalesAgentTasks_Status CHECK (TaskStatus IN ('Open','InProgress','Done','Cancelled'))
);
GO

/* Audit record for staff cancellation override (intentional OrderDesk-only capability). */
IF OBJECT_ID('dbo.CancellationOverrides','U') IS NULL
CREATE TABLE dbo.CancellationOverrides
(
    CancellationOverrideId INT IDENTITY(1,1) CONSTRAINT PK_CancellationOverrides PRIMARY KEY,
    OrderId         INT           NOT NULL,
    SalesAgentId    INT           NOT NULL,
    PreviousStatus  NVARCHAR(20)  NOT NULL,
    Justification   NVARCHAR(600) NOT NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_CancellationOverrides_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_CancellationOverrides_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT FK_CancellationOverrides_Agent FOREIGN KEY (SalesAgentId) REFERENCES dbo.SalesAgents(SalesAgentId)
);
GO
