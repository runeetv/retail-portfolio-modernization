/* ============================================================================
   LegacyRetailDB - CustomerPortal-owned tables
   ============================================================================ */
SET NOCOUNT ON;
GO

/* CustomerAccount is a DIFFERENT C# model than Customer but maps 1:1 to a Customer
   row via CustomerId (intentional: different domain models, related SQL tables). */
IF OBJECT_ID('dbo.CustomerAccounts','U') IS NULL
CREATE TABLE dbo.CustomerAccounts
(
    CustomerAccountId INT IDENTITY(1,1) CONSTRAINT PK_CustomerAccounts PRIMARY KEY,
    CustomerId      INT           NOT NULL,
    Username        NVARCHAR(100) NOT NULL,
    PasswordHash    NVARCHAR(200) NOT NULL,   -- dev-friendly local auth (PBKDF2)
    IsLocked        BIT           NOT NULL CONSTRAINT DF_CustomerAccounts_Locked DEFAULT(0),
    LastLoginUtc    DATETIME2(0)  NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_CustomerAccounts_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_CustomerAccounts_Username UNIQUE (Username),
    CONSTRAINT UQ_CustomerAccounts_Customer UNIQUE (CustomerId),
    CONSTRAINT FK_CustomerAccounts_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
);
GO

IF OBJECT_ID('dbo.CustomerPreferences','U') IS NULL
CREATE TABLE dbo.CustomerPreferences
(
    CustomerPreferenceId INT IDENTITY(1,1) CONSTRAINT PK_CustomerPreferences PRIMARY KEY,
    CustomerAccountId INT         NOT NULL,
    PrefKey         NVARCHAR(60)  NOT NULL,
    PrefValue       NVARCHAR(200) NOT NULL,
    CONSTRAINT UQ_CustomerPreferences UNIQUE (CustomerAccountId, PrefKey),
    CONSTRAINT FK_CustomerPreferences_Account FOREIGN KEY (CustomerAccountId) REFERENCES dbo.CustomerAccounts(CustomerAccountId)
);
GO

IF OBJECT_ID('dbo.ShoppingCarts','U') IS NULL
CREATE TABLE dbo.ShoppingCarts
(
    ShoppingCartId  INT IDENTITY(1,1) CONSTRAINT PK_ShoppingCarts PRIMARY KEY,
    CustomerAccountId INT         NOT NULL,
    CartStatus      NVARCHAR(20)  NOT NULL CONSTRAINT DF_ShoppingCarts_Status DEFAULT('Active'),
    AppliedCouponCode NVARCHAR(40) NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_ShoppingCarts_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CheckedOutUtc   DATETIME2(0)  NULL,
    CONSTRAINT FK_ShoppingCarts_Account FOREIGN KEY (CustomerAccountId) REFERENCES dbo.CustomerAccounts(CustomerAccountId),
    CONSTRAINT CK_ShoppingCarts_Status CHECK (CartStatus IN ('Active','CheckedOut','Abandoned'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShoppingCarts_Account' AND object_id=OBJECT_ID('dbo.ShoppingCarts'))
CREATE INDEX IX_ShoppingCarts_Account ON dbo.ShoppingCarts(CustomerAccountId, CartStatus);
GO

IF OBJECT_ID('dbo.ShoppingCartItems','U') IS NULL
CREATE TABLE dbo.ShoppingCartItems
(
    ShoppingCartItemId INT IDENTITY(1,1) CONSTRAINT PK_ShoppingCartItems PRIMARY KEY,
    ShoppingCartId  INT           NOT NULL,
    ProductId       INT           NOT NULL,
    Quantity        INT           NOT NULL,
    AddedUtc        DATETIME2(0)  NOT NULL CONSTRAINT DF_ShoppingCartItems_AddedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_ShoppingCartItems UNIQUE (ShoppingCartId, ProductId),
    CONSTRAINT FK_ShoppingCartItems_Cart FOREIGN KEY (ShoppingCartId) REFERENCES dbo.ShoppingCarts(ShoppingCartId),
    CONSTRAINT FK_ShoppingCartItems_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_ShoppingCartItems_Qty CHECK (Quantity > 0)
);
GO

IF OBJECT_ID('dbo.Wishlists','U') IS NULL
CREATE TABLE dbo.Wishlists
(
    WishlistId      INT IDENTITY(1,1) CONSTRAINT PK_Wishlists PRIMARY KEY,
    CustomerAccountId INT         NOT NULL,
    Name            NVARCHAR(100) NOT NULL CONSTRAINT DF_Wishlists_Name DEFAULT('My Wishlist'),
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_Wishlists_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Wishlists_Account FOREIGN KEY (CustomerAccountId) REFERENCES dbo.CustomerAccounts(CustomerAccountId)
);
GO

IF OBJECT_ID('dbo.WishlistItems','U') IS NULL
CREATE TABLE dbo.WishlistItems
(
    WishlistItemId  INT IDENTITY(1,1) CONSTRAINT PK_WishlistItems PRIMARY KEY,
    WishlistId      INT           NOT NULL,
    ProductId       INT           NOT NULL,
    AddedUtc        DATETIME2(0)  NOT NULL CONSTRAINT DF_WishlistItems_AddedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_WishlistItems UNIQUE (WishlistId, ProductId),
    CONSTRAINT FK_WishlistItems_Wishlist FOREIGN KEY (WishlistId) REFERENCES dbo.Wishlists(WishlistId),
    CONSTRAINT FK_WishlistItems_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
);
GO

IF OBJECT_ID('dbo.Coupons','U') IS NULL
CREATE TABLE dbo.Coupons
(
    CouponId        INT IDENTITY(1,1) CONSTRAINT PK_Coupons PRIMARY KEY,
    Code            NVARCHAR(40)  NOT NULL,
    CouponType      NVARCHAR(20)  NOT NULL,   -- 'Percent' | 'FixedAmount'
    Value           DECIMAL(18,2) NOT NULL,
    MinCartTotal    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Coupons_MinCart DEFAULT(0),
    RequiredStatus  NVARCHAR(20)  NULL,       -- e.g. only 'VIP' may use
    MaxRedemptions  INT           NULL,
    ExpiresUtc      DATETIME2(0)  NULL,
    IsActive        BIT           NOT NULL CONSTRAINT DF_Coupons_Active DEFAULT(1),
    CONSTRAINT UQ_Coupons_Code UNIQUE (Code),
    CONSTRAINT CK_Coupons_Type CHECK (CouponType IN ('Percent','FixedAmount')),
    CONSTRAINT CK_Coupons_Value CHECK (Value >= 0)
);
GO

IF OBJECT_ID('dbo.CouponRedemptions','U') IS NULL
CREATE TABLE dbo.CouponRedemptions
(
    CouponRedemptionId INT IDENTITY(1,1) CONSTRAINT PK_CouponRedemptions PRIMARY KEY,
    CouponId        INT           NOT NULL,
    CustomerAccountId INT         NOT NULL,
    OrderId         INT           NULL,
    AmountDiscounted DECIMAL(18,2) NOT NULL,
    RedeemedUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_CouponRedemptions_RedeemedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_CouponRedemptions_Coupon FOREIGN KEY (CouponId) REFERENCES dbo.Coupons(CouponId),
    CONSTRAINT FK_CouponRedemptions_Account FOREIGN KEY (CustomerAccountId) REFERENCES dbo.CustomerAccounts(CustomerAccountId),
    CONSTRAINT FK_CouponRedemptions_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId)
);
GO

IF OBJECT_ID('dbo.SupportTickets','U') IS NULL
CREATE TABLE dbo.SupportTickets
(
    SupportTicketId INT IDENTITY(1,1) CONSTRAINT PK_SupportTickets PRIMARY KEY,
    CustomerAccountId INT         NOT NULL,
    Subject         NVARCHAR(200) NOT NULL,
    TicketStatus    NVARCHAR(20)  NOT NULL CONSTRAINT DF_SupportTickets_Status DEFAULT('Open'),
    RelatedOrderId  INT           NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SupportTickets_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SupportTickets_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_SupportTickets_Account FOREIGN KEY (CustomerAccountId) REFERENCES dbo.CustomerAccounts(CustomerAccountId),
    CONSTRAINT FK_SupportTickets_Order FOREIGN KEY (RelatedOrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT CK_SupportTickets_Status CHECK (TicketStatus IN ('Open','Pending','Resolved','Closed'))
);
GO

IF OBJECT_ID('dbo.SupportTicketMessages','U') IS NULL
CREATE TABLE dbo.SupportTicketMessages
(
    SupportTicketMessageId INT IDENTITY(1,1) CONSTRAINT PK_SupportTicketMessages PRIMARY KEY,
    SupportTicketId INT           NOT NULL,
    Author          NVARCHAR(100) NOT NULL,   -- 'Customer' or agent name
    Body            NVARCHAR(2000) NOT NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL CONSTRAINT DF_SupportTicketMessages_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_SupportTicketMessages_Ticket FOREIGN KEY (SupportTicketId) REFERENCES dbo.SupportTickets(SupportTicketId)
);
GO
