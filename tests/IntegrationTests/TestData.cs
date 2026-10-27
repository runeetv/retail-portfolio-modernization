using LegacyRetail.Common;
using Microsoft.Data.SqlClient;

namespace IntegrationTests;

/// <summary>Creates isolated rows directly in the shared DB so each test is self-contained.</summary>
public sealed class TestData
{
    private readonly SqlConnection _c;
    public TestData(SqlConnection conn) => _c = conn;

    private int Scalar(string sql, params (string, object?)[] ps)
    {
        using var cmd = new SqlCommand(sql, _c);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    private void Exec(string sql, params (string, object?)[] ps)
    {
        using var cmd = new SqlCommand(sql, _c);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static string Uniq() => Guid.NewGuid().ToString("N")[..10];

    public int CreateCustomer(bool withShipping = true, string status = "Standard")
    {
        var u = Uniq();
        return Scalar(@"
INSERT dbo.Customers (FirstName,LastName,Email,Phone,ShipAddressLine,ShipCity,ShipRegion,ShipPostalCode,ShipCountry,CustomerStatus)
OUTPUT INSERTED.CustomerId
VALUES ('Test','User',@em,'555-0100',@addr,'TestCity','CA','90001','USA',@st)",
            ("@em", $"test.{u}@example.test"),
            ("@addr", withShipping ? "1 Test St" : (object?)DBNull.Value),
            ("@st", status));
    }

    public int CreateCustomerAccount(int customerId, out string username)
    {
        username = $"it_{Uniq()}";
        var hash = PasswordHasher.HashDeterministic("Password123!", 7);
        return Scalar("INSERT dbo.CustomerAccounts (CustomerId,Username,PasswordHash) OUTPUT INSERTED.CustomerAccountId VALUES (@c,@u,@h)",
            ("@c", customerId), ("@u", username), ("@h", hash));
    }

    public int EnsureCategory()
    {
        var existing = Scalar("SELECT TOP 1 ISNULL((SELECT TOP 1 CategoryId FROM dbo.ProductCategories ORDER BY CategoryId),0)");
        if (existing > 0) return existing;
        return Scalar("INSERT dbo.ProductCategories (Name,Slug) OUTPUT INSERTED.CategoryId VALUES (@n,@s)",
            ("@n", "TestCat"), ("@s", "testcat-" + Uniq()));
    }

    public int CreateProduct(decimal price = 50m, int stock = 100)
    {
        var cat = EnsureCategory();
        var pid = Scalar("INSERT dbo.Products (Sku,Name,CategoryId,UnitPrice) OUTPUT INSERTED.ProductId VALUES (@sku,@n,@c,@p)",
            ("@sku", "IT-" + Uniq()), ("@n", "Test Product"), ("@c", cat), ("@p", price));
        Exec("INSERT dbo.Inventory (ProductId,QuantityOnHand,QuantityAllocated,ReorderLevel) VALUES (@p,@q,0,5)",
            ("@p", pid), ("@q", stock));
        return pid;
    }

    public int SalesAgent(bool canOverride = false)
    {
        return Scalar(@"INSERT dbo.SalesAgents (AgentCode,FullName,Email,CommissionRate,CanOverrideCancellation)
OUTPUT INSERTED.SalesAgentId VALUES (@code,'Test Agent',@em,0.03,@o)",
            ("@code", "IT-" + Uniq()), ("@em", $"agent.{Uniq()}@retail.test"), ("@o", canOverride ? 1 : 0));
    }

    /// <summary>Creates an order in the given status with one line for the product.</summary>
    public int CreateOrder(int customerId, int productId, string status, string source = "CustomerPortal", int qty = 2, decimal unit = 50m)
    {
        var oid = Scalar(@"
INSERT dbo.Orders (OrderNumber,CustomerId,OrderStatus,OrderSource,SubTotal,DiscountTotal,GrandTotal,PlacedUtc)
OUTPUT INSERTED.OrderId
VALUES (@no,@c,@st,@src,@sub,0,@sub,SYSUTCDATETIME())",
            ("@no", "IT-ORD-" + Uniq()), ("@c", customerId), ("@st", status), ("@src", source), ("@sub", unit * qty));
        Exec("INSERT dbo.OrderLines (OrderId,ProductId,Quantity,UnitPrice,LineDiscount) VALUES (@o,@p,@q,@u,0)",
            ("@o", oid), ("@p", productId), ("@q", qty), ("@u", unit));
        return oid;
    }

    public int CreateReturn(int orderId, int customerId, int productId, string status, int qty = 1)
    {
        var rid = Scalar(@"INSERT dbo.Returns (ReturnNumber,OrderId,CustomerId,ReturnStatus,Reason)
OUTPUT INSERTED.ReturnId VALUES (@no,@o,@c,@st,'test')",
            ("@no", "IT-RET-" + Uniq()), ("@o", orderId), ("@c", customerId), ("@st", status));
        Exec("INSERT dbo.ReturnLines (ReturnId,ProductId,Quantity) VALUES (@r,@p,@q)",
            ("@r", rid), ("@p", productId), ("@q", qty));
        return rid;
    }

    public int Warehouse()
        => Scalar("INSERT dbo.Warehouses (Code,Name,Region) OUTPUT INSERTED.WarehouseId VALUES (@c,'Test WH','Test')",
            ("@c", "ITWH-" + Uniq()[..6]));

    public (int onHand, int allocated) GetInventory(int productId)
    {
        using var cmd = new SqlCommand("SELECT QuantityOnHand, QuantityAllocated FROM dbo.Inventory WHERE ProductId=@p", _c);
        cmd.Parameters.AddWithValue("@p", productId);
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.GetInt32(0), r.GetInt32(1));
    }

    public string GetOrderStatus(int orderId)
    {
        using var cmd = new SqlCommand("SELECT OrderStatus FROM dbo.Orders WHERE OrderId=@o", _c);
        cmd.Parameters.AddWithValue("@o", orderId);
        return (string)cmd.ExecuteScalar()!;
    }

    public string GetReturnStatus(int returnId)
    {
        using var cmd = new SqlCommand("SELECT ReturnStatus FROM dbo.Returns WHERE ReturnId=@r", _c);
        cmd.Parameters.AddWithValue("@r", returnId);
        return (string)cmd.ExecuteScalar()!;
    }
}
