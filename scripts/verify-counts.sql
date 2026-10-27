SET NOCOUNT ON;
SELECT 'Customers' AS TableName, COUNT(*) AS Rows FROM dbo.Customers
UNION ALL SELECT 'CustomerAccounts', COUNT(*) FROM dbo.CustomerAccounts
UNION ALL SELECT 'ProductCategories', COUNT(*) FROM dbo.ProductCategories
UNION ALL SELECT 'Products', COUNT(*) FROM dbo.Products
UNION ALL SELECT 'Inventory', COUNT(*) FROM dbo.Inventory
UNION ALL SELECT 'Orders', COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderLines', COUNT(*) FROM dbo.OrderLines
UNION ALL SELECT 'SalesAgents', COUNT(*) FROM dbo.SalesAgents
UNION ALL SELECT 'SalesQuotes', COUNT(*) FROM dbo.SalesQuotes
UNION ALL SELECT 'SalesQuoteLines', COUNT(*) FROM dbo.SalesQuoteLines
UNION ALL SELECT 'DiscountApprovals', COUNT(*) FROM dbo.DiscountApprovals
UNION ALL SELECT 'SalesCommissions', COUNT(*) FROM dbo.SalesCommissions
UNION ALL SELECT 'SalesAgentTasks', COUNT(*) FROM dbo.SalesAgentTasks
UNION ALL SELECT 'ShoppingCarts', COUNT(*) FROM dbo.ShoppingCarts
UNION ALL SELECT 'ShoppingCartItems', COUNT(*) FROM dbo.ShoppingCartItems
UNION ALL SELECT 'Wishlists', COUNT(*) FROM dbo.Wishlists
UNION ALL SELECT 'Coupons', COUNT(*) FROM dbo.Coupons
UNION ALL SELECT 'CouponRedemptions', COUNT(*) FROM dbo.CouponRedemptions
UNION ALL SELECT 'SupportTickets', COUNT(*) FROM dbo.SupportTickets
UNION ALL SELECT 'Warehouses', COUNT(*) FROM dbo.Warehouses
UNION ALL SELECT 'WarehouseBins', COUNT(*) FROM dbo.WarehouseBins
UNION ALL SELECT 'InventoryAllocations', COUNT(*) FROM dbo.InventoryAllocations
UNION ALL SELECT 'PickLists', COUNT(*) FROM dbo.PickLists
UNION ALL SELECT 'Shipments', COUNT(*) FROM dbo.Shipments
UNION ALL SELECT 'CarrierEvents', COUNT(*) FROM dbo.CarrierEvents
UNION ALL SELECT 'Returns', COUNT(*) FROM dbo.Returns
UNION ALL SELECT 'ReturnReceipts', COUNT(*) FROM dbo.ReturnReceipts
ORDER BY TableName;
