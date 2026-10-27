/* ============================================================================
   LegacyRetailDB - FulfillmentAPI-owned tables
   ============================================================================ */
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.Warehouses','U') IS NULL
CREATE TABLE dbo.Warehouses
(
    WarehouseId     INT IDENTITY(1,1) CONSTRAINT PK_Warehouses PRIMARY KEY,
    Code            NVARCHAR(20)  NOT NULL,
    Name            NVARCHAR(120) NOT NULL,
    Region          NVARCHAR(80)  NULL,
    IsActive        BIT           NOT NULL CONSTRAINT DF_Warehouses_Active DEFAULT(1),
    CONSTRAINT UQ_Warehouses_Code UNIQUE (Code)
);
GO

IF OBJECT_ID('dbo.WarehouseBins','U') IS NULL
CREATE TABLE dbo.WarehouseBins
(
    WarehouseBinId  INT IDENTITY(1,1) CONSTRAINT PK_WarehouseBins PRIMARY KEY,
    WarehouseId     INT           NOT NULL,
    BinCode         NVARCHAR(30)  NOT NULL,
    Aisle           NVARCHAR(10)  NULL,
    CONSTRAINT UQ_WarehouseBins UNIQUE (WarehouseId, BinCode),
    CONSTRAINT FK_WarehouseBins_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses(WarehouseId)
);
GO

/* Allocation reserves stock against an order line. Cannot exceed available. */
IF OBJECT_ID('dbo.InventoryAllocations','U') IS NULL
CREATE TABLE dbo.InventoryAllocations
(
    InventoryAllocationId INT IDENTITY(1,1) CONSTRAINT PK_InventoryAllocations PRIMARY KEY,
    OrderId         INT           NOT NULL,
    ProductId       INT           NOT NULL,
    WarehouseId     INT           NOT NULL,
    QuantityAllocated INT         NOT NULL,
    AllocationStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_InventoryAllocations_Status DEFAULT('Allocated'),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_InventoryAllocations_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_InventoryAllocations_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT FK_InventoryAllocations_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT FK_InventoryAllocations_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses(WarehouseId),
    CONSTRAINT CK_InventoryAllocations_Qty CHECK (QuantityAllocated > 0),
    CONSTRAINT CK_InventoryAllocations_Status CHECK (AllocationStatus IN ('Allocated','Released','Consumed'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InventoryAllocations_Order' AND object_id=OBJECT_ID('dbo.InventoryAllocations'))
CREATE INDEX IX_InventoryAllocations_Order ON dbo.InventoryAllocations(OrderId);
GO

IF OBJECT_ID('dbo.PickLists','U') IS NULL
CREATE TABLE dbo.PickLists
(
    PickListId      INT IDENTITY(1,1) CONSTRAINT PK_PickLists PRIMARY KEY,
    OrderId         INT           NOT NULL,
    WarehouseId     INT           NOT NULL,
    PickStatus      NVARCHAR(20)  NOT NULL CONSTRAINT DF_PickLists_Status DEFAULT('Created'),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_PickLists_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_PickLists_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT FK_PickLists_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses(WarehouseId),
    CONSTRAINT CK_PickLists_Status CHECK (PickStatus IN ('Created','InProgress','Completed'))
);
GO

IF OBJECT_ID('dbo.PickListItems','U') IS NULL
CREATE TABLE dbo.PickListItems
(
    PickListItemId  INT IDENTITY(1,1) CONSTRAINT PK_PickListItems PRIMARY KEY,
    PickListId      INT           NOT NULL,
    ProductId       INT           NOT NULL,
    WarehouseBinId  INT           NULL,
    QuantityToPick  INT           NOT NULL,
    QuantityPicked  INT           NOT NULL CONSTRAINT DF_PickListItems_Picked DEFAULT(0),
    IsPicked        BIT           NOT NULL CONSTRAINT DF_PickListItems_IsPicked DEFAULT(0),
    CONSTRAINT FK_PickListItems_PickList FOREIGN KEY (PickListId) REFERENCES dbo.PickLists(PickListId),
    CONSTRAINT FK_PickListItems_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT FK_PickListItems_Bin FOREIGN KEY (WarehouseBinId) REFERENCES dbo.WarehouseBins(WarehouseBinId),
    CONSTRAINT CK_PickListItems_Qty CHECK (QuantityToPick > 0 AND QuantityPicked >= 0 AND QuantityPicked <= QuantityToPick)
);
GO

IF OBJECT_ID('dbo.Shipments','U') IS NULL
CREATE TABLE dbo.Shipments
(
    ShipmentId      INT IDENTITY(1,1) CONSTRAINT PK_Shipments PRIMARY KEY,
    OrderId         INT           NOT NULL,
    ShipmentStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_Shipments_Status DEFAULT('Created'),
    Carrier         NVARCHAR(60)  NULL,
    TrackingNumber  NVARCHAR(80)  NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_Shipments_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    DispatchedUtc   DATETIME2(0)  NULL,
    CONSTRAINT FK_Shipments_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT CK_Shipments_Status CHECK (ShipmentStatus IN ('Created','Packed','Dispatched','InTransit','Delivered','Exception'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Shipments_Order' AND object_id=OBJECT_ID('dbo.Shipments'))
CREATE INDEX IX_Shipments_Order ON dbo.Shipments(OrderId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Shipments_Tracking' AND object_id=OBJECT_ID('dbo.Shipments'))
CREATE INDEX IX_Shipments_Tracking ON dbo.Shipments(TrackingNumber) WHERE TrackingNumber IS NOT NULL;
GO

IF OBJECT_ID('dbo.ShipmentPackages','U') IS NULL
CREATE TABLE dbo.ShipmentPackages
(
    ShipmentPackageId INT IDENTITY(1,1) CONSTRAINT PK_ShipmentPackages PRIMARY KEY,
    ShipmentId      INT           NOT NULL,
    PackageCode     NVARCHAR(40)  NOT NULL,
    WeightKg        DECIMAL(9,3)  NULL,
    CONSTRAINT UQ_ShipmentPackages_Code UNIQUE (PackageCode),
    CONSTRAINT FK_ShipmentPackages_Shipment FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipments(ShipmentId)
);
GO

/* CarrierEvents must be idempotent on (ShipmentId, EventCode, EventUtc). */
IF OBJECT_ID('dbo.CarrierEvents','U') IS NULL
CREATE TABLE dbo.CarrierEvents
(
    CarrierEventId  INT IDENTITY(1,1) CONSTRAINT PK_CarrierEvents PRIMARY KEY,
    ShipmentId      INT           NOT NULL,
    EventCode       NVARCHAR(40)  NOT NULL,
    Description     NVARCHAR(200) NULL,
    EventUtc        DATETIME2(0)  NOT NULL,
    RecordedUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_CarrierEvents_RecordedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_CarrierEvents UNIQUE (ShipmentId, EventCode, EventUtc),
    CONSTRAINT FK_CarrierEvents_Shipment FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipments(ShipmentId)
);
GO

/* Warehouse receipt of returned goods. Does NOT imply financial refund approval. */
IF OBJECT_ID('dbo.ReturnReceipts','U') IS NULL
CREATE TABLE dbo.ReturnReceipts
(
    ReturnReceiptId INT IDENTITY(1,1) CONSTRAINT PK_ReturnReceipts PRIMARY KEY,
    ReturnId        INT           NOT NULL,
    WarehouseId     INT           NOT NULL,
    ReceivedStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_ReturnReceipts_Status DEFAULT('Received'),
    ReceivedUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_ReturnReceipts_ReceivedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ReturnReceipts_Return UNIQUE (ReturnId),
    CONSTRAINT FK_ReturnReceipts_Return FOREIGN KEY (ReturnId) REFERENCES dbo.Returns(ReturnId),
    CONSTRAINT FK_ReturnReceipts_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses(WarehouseId),
    CONSTRAINT CK_ReturnReceipts_Status CHECK (ReceivedStatus IN ('Received','Inspected','Restocked'))
);
GO

IF OBJECT_ID('dbo.WarehouseAdjustments','U') IS NULL
CREATE TABLE dbo.WarehouseAdjustments
(
    WarehouseAdjustmentId INT IDENTITY(1,1) CONSTRAINT PK_WarehouseAdjustments PRIMARY KEY,
    ProductId       INT           NOT NULL,
    WarehouseId     INT           NOT NULL,
    QuantityDelta   INT           NOT NULL,
    Reason          NVARCHAR(200) NOT NULL,
    RelatedReturnReceiptId INT    NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_WarehouseAdjustments_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_WarehouseAdjustments_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT FK_WarehouseAdjustments_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses(WarehouseId),
    CONSTRAINT FK_WarehouseAdjustments_Receipt FOREIGN KEY (RelatedReturnReceiptId) REFERENCES dbo.ReturnReceipts(ReturnReceiptId)
);
GO
