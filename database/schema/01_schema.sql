/* ============================================================================
   LegacyRetailDB - Core schema
   Single physical database shared by OrderDesk, CustomerPortal, FulfillmentAPI.
   Ownership is logical (see benchmark ground-truth), not enforced by DB schema.
   This script is idempotent: it only creates objects that do not already exist.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ------------------------------------------------------------------ SHARED */

IF OBJECT_ID('dbo.ProductCategories','U') IS NULL
CREATE TABLE dbo.ProductCategories
(
    CategoryId      INT IDENTITY(1,1) CONSTRAINT PK_ProductCategories PRIMARY KEY,
    Name            NVARCHAR(80)  NOT NULL,
    Slug            NVARCHAR(100) NOT NULL,
    IsActive        BIT           NOT NULL CONSTRAINT DF_ProductCategories_IsActive DEFAULT(1),
    CONSTRAINT UQ_ProductCategories_Slug UNIQUE (Slug)
);
GO

IF OBJECT_ID('dbo.Products','U') IS NULL
CREATE TABLE dbo.Products
(
    ProductId       INT IDENTITY(1,1) CONSTRAINT PK_Products PRIMARY KEY,
    Sku             NVARCHAR(40)  NOT NULL,
    Name            NVARCHAR(160) NOT NULL,
    CategoryId      INT           NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    IsDiscontinued  BIT           NOT NULL CONSTRAINT DF_Products_IsDiscontinued DEFAULT(0),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_Products_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Products_Sku UNIQUE (Sku),
    CONSTRAINT CK_Products_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES dbo.ProductCategories(CategoryId)
);
GO

IF OBJECT_ID('dbo.Customers','U') IS NULL
CREATE TABLE dbo.Customers
(
    CustomerId      INT IDENTITY(1,1) CONSTRAINT PK_Customers PRIMARY KEY,
    FirstName       NVARCHAR(80)  NOT NULL,
    LastName        NVARCHAR(80)  NOT NULL,
    Email           NVARCHAR(200) NOT NULL,
    Phone           NVARCHAR(40)  NULL,
    /* Shipping address lives on the customer record (read by FulfillmentAPI) */
    ShipAddressLine NVARCHAR(200) NULL,
    ShipCity        NVARCHAR(100) NULL,
    ShipRegion      NVARCHAR(100) NULL,
    ShipPostalCode  NVARCHAR(20)  NULL,
    ShipCountry     NVARCHAR(80)  NULL,
    CustomerStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_Customers_Status DEFAULT('Standard'),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_Customers_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email),
    CONSTRAINT CK_Customers_Status CHECK (CustomerStatus IN ('Standard','Preferred','VIP','Blocked'))
);
GO

IF OBJECT_ID('dbo.Inventory','U') IS NULL
CREATE TABLE dbo.Inventory
(
    ProductId       INT           NOT NULL CONSTRAINT PK_Inventory PRIMARY KEY,
    QuantityOnHand  INT           NOT NULL CONSTRAINT DF_Inventory_OnHand DEFAULT(0),
    QuantityAllocated INT         NOT NULL CONSTRAINT DF_Inventory_Alloc DEFAULT(0),
    ReorderLevel    INT           NOT NULL CONSTRAINT DF_Inventory_Reorder DEFAULT(10),
    RowVersion      ROWVERSION,
    CONSTRAINT FK_Inventory_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_Inventory_OnHand CHECK (QuantityOnHand >= 0),
    CONSTRAINT CK_Inventory_Alloc CHECK (QuantityAllocated >= 0 AND QuantityAllocated <= QuantityOnHand)
);
GO

/* Orders may originate from OrderDesk (staff) or CustomerPortal (web checkout).
   FulfillmentAPI processes eligible orders regardless of origin. */
IF OBJECT_ID('dbo.Orders','U') IS NULL
CREATE TABLE dbo.Orders
(
    OrderId         INT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    OrderNumber     NVARCHAR(30)  NOT NULL,
    CustomerId      INT           NOT NULL,
    OrderStatus     NVARCHAR(20)  NOT NULL CONSTRAINT DF_Orders_Status DEFAULT('Pending'),
    OrderSource     NVARCHAR(20)  NOT NULL,   -- 'OrderDesk' | 'CustomerPortal'
    SubTotal        DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_SubTotal DEFAULT(0),
    DiscountTotal   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_DiscountTotal DEFAULT(0),
    GrandTotal      DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_GrandTotal DEFAULT(0),
    PlacedByAgentId INT           NULL,       -- set when OrderDesk staff places order
    SourceQuoteId   INT           NULL,       -- set when converted from a SalesQuote
    IdempotencyKey  NVARCHAR(80)  NULL,       -- used by CustomerPortal checkout
    CancelReason    NVARCHAR(400) NULL,
    PlacedUtc       DATETIME2(0)  NOT NULL CONSTRAINT DF_Orders_PlacedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Orders_OrderNumber UNIQUE (OrderNumber),
    CONSTRAINT FK_Orders_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT CK_Orders_Status CHECK (OrderStatus IN ('Pending','Confirmed','Allocated','Picking','Packed','Shipped','Delivered','Cancelled')),
    CONSTRAINT CK_Orders_Source CHECK (OrderSource IN ('OrderDesk','CustomerPortal')),
    CONSTRAINT CK_Orders_Totals CHECK (SubTotal >= 0 AND DiscountTotal >= 0 AND GrandTotal >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_Orders_Idempotency' AND object_id=OBJECT_ID('dbo.Orders'))
CREATE UNIQUE INDEX UQ_Orders_Idempotency ON dbo.Orders(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Orders_Customer' AND object_id=OBJECT_ID('dbo.Orders'))
CREATE INDEX IX_Orders_Customer ON dbo.Orders(CustomerId, OrderStatus);
GO

IF OBJECT_ID('dbo.OrderLines','U') IS NULL
CREATE TABLE dbo.OrderLines
(
    OrderLineId     INT IDENTITY(1,1) CONSTRAINT PK_OrderLines PRIMARY KEY,
    OrderId         INT           NOT NULL,
    ProductId       INT           NOT NULL,
    Quantity        INT           NOT NULL,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    LineDiscount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderLines_Disc DEFAULT(0),
    CONSTRAINT FK_OrderLines_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT FK_OrderLines_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_OrderLines_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_OrderLines_Price CHECK (UnitPrice >= 0 AND LineDiscount >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OrderLines_Order' AND object_id=OBJECT_ID('dbo.OrderLines'))
CREATE INDEX IX_OrderLines_Order ON dbo.OrderLines(OrderId);
GO

/* Returns: created by CustomerPortal, approved in OrderDesk, received by FulfillmentAPI.
   NOTE terminology inconsistency (intentional): Returns.ReturnStatus uses 'Requested'
   whereas FulfillmentAPI ReturnReceipts use 'Received'. */
IF OBJECT_ID('dbo.Returns','U') IS NULL
CREATE TABLE dbo.Returns
(
    ReturnId        INT IDENTITY(1,1) CONSTRAINT PK_Returns PRIMARY KEY,
    ReturnNumber    NVARCHAR(30)  NOT NULL,
    OrderId         INT           NOT NULL,
    CustomerId      INT           NOT NULL,
    ReturnStatus    NVARCHAR(20)  NOT NULL CONSTRAINT DF_Returns_Status DEFAULT('Requested'),
    Reason          NVARCHAR(400) NULL,
    RequestedUtc    DATETIME2(0)  NOT NULL CONSTRAINT DF_Returns_RequestedUtc DEFAULT (SYSUTCDATETIME()),
    ReviewedByAgentId INT         NULL,
    ReviewedUtc     DATETIME2(0)  NULL,
    CONSTRAINT UQ_Returns_ReturnNumber UNIQUE (ReturnNumber),
    CONSTRAINT FK_Returns_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT FK_Returns_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT CK_Returns_Status CHECK (ReturnStatus IN ('Requested','Approved','Rejected','Received','Closed'))
);
GO

IF OBJECT_ID('dbo.ReturnLines','U') IS NULL
CREATE TABLE dbo.ReturnLines
(
    ReturnLineId    INT IDENTITY(1,1) CONSTRAINT PK_ReturnLines PRIMARY KEY,
    ReturnId        INT           NOT NULL,
    ProductId       INT           NOT NULL,
    Quantity        INT           NOT NULL,
    CONSTRAINT FK_ReturnLines_Return FOREIGN KEY (ReturnId) REFERENCES dbo.Returns(ReturnId),
    CONSTRAINT FK_ReturnLines_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_ReturnLines_Qty CHECK (Quantity > 0)
);
GO
