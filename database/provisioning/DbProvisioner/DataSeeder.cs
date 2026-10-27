using LegacyRetail.Common;
using Microsoft.Data.SqlClient;

namespace DbProvisioner;

/// <summary>
/// Deterministic, idempotent sample data seeder. Uses a fixed RNG seed so reruns
/// produce identical data. If core tables already contain seed data, seeding is
/// skipped (safe to rerun). Use --reset to rebuild from scratch.
/// </summary>
public sealed class DataSeeder
{
    private readonly string _connStr;
    private readonly Random _rng = new(20260101); // fixed seed -> deterministic

    public DataSeeder(string connectionString) => _connStr = connectionString;

    public void Seed()
    {
        using var conn = new SqlConnection(_connStr);
        conn.Open();

        if (AlreadySeeded(conn))
        {
            Console.WriteLine("  (data already present - skipping seed; use --reset to rebuild)");
            return;
        }

        var categoryIds = SeedCategories(conn);
        var productIds = SeedProducts(conn, categoryIds);
        SeedInventory(conn, productIds);
        var customerIds = SeedCustomers(conn);
        var accountIds = SeedCustomerAccounts(conn, customerIds);
        SeedPreferencesAndWishlists(conn, accountIds, productIds);
        var agentIds = SeedSalesAgents(conn);
        var warehouseIds = SeedWarehouses(conn, out var binIds);
        var couponIds = SeedCoupons(conn);

        var orderIds = SeedOrders(conn, customerIds, productIds, agentIds);
        SeedQuotes(conn, customerIds, productIds, agentIds, orderIds);
        SeedCommissionsAndTasks(conn, agentIds, customerIds, orderIds);
        SeedCarts(conn, accountIds, productIds, couponIds);
        SeedFulfillment(conn, orderIds, productIds, warehouseIds, binIds);
        SeedReturns(conn, orderIds, customerIds, productIds, warehouseIds);
        SeedSupportTickets(conn, accountIds, orderIds);

        Console.WriteLine("  seed complete.");
    }

    private bool AlreadySeeded(SqlConnection conn)
    {
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Customers", conn);
        return (int)cmd.ExecuteScalar()! > 0;
    }

    private static int ExecScalarInt(SqlConnection c, string sql, params (string, object)[] ps)
    {
        using var cmd = new SqlCommand(sql, c);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void Exec(SqlConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = new SqlCommand(sql, c);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- Categories
    private static readonly string[] CategoryNames =
        { "Electronics", "Home & Kitchen", "Apparel", "Sports & Outdoors",
          "Books & Media", "Toys & Games", "Health & Beauty", "Office Supplies" };

    private List<int> SeedCategories(SqlConnection conn)
    {
        var ids = new List<int>();
        foreach (var name in CategoryNames)
        {
            var slug = name.ToLowerInvariant().Replace(" & ", "-").Replace(" ", "-");
            ids.Add(ExecScalarInt(conn,
                "INSERT dbo.ProductCategories (Name, Slug) OUTPUT INSERTED.CategoryId VALUES (@n,@s)",
                ("@n", name), ("@s", slug)));
        }
        return ids;
    }

    // ------------------------------------------------------------------ Products
    private List<int> SeedProducts(SqlConnection conn, List<int> categoryIds)
    {
        string[] adjectives = { "Classic", "Pro", "Deluxe", "Compact", "Ultra", "Eco", "Smart", "Premium", "Essential", "Rugged" };
        string[] nouns = { "Widget", "Gadget", "Organizer", "Bottle", "Lamp", "Backpack", "Notebook", "Speaker", "Blender", "Charger", "Jacket", "Chair" };
        var ids = new List<int>();
        for (int i = 1; i <= 120; i++)
        {
            var cat = categoryIds[(i - 1) % categoryIds.Count];
            var name = $"{adjectives[_rng.Next(adjectives.Length)]} {nouns[_rng.Next(nouns.Length)]} {i}";
            var sku = $"SKU-{i:0000}";
            var price = Math.Round((decimal)(_rng.NextDouble() * 480 + 5), 2);
            var discontinued = (i % 23 == 0) ? 1 : 0;
            ids.Add(ExecScalarInt(conn,
                "INSERT dbo.Products (Sku, Name, CategoryId, UnitPrice, IsDiscontinued) OUTPUT INSERTED.ProductId VALUES (@sku,@n,@c,@p,@d)",
                ("@sku", sku), ("@n", name), ("@c", cat), ("@p", price), ("@d", discontinued)));
        }
        return ids;
    }

    private void SeedInventory(SqlConnection conn, List<int> productIds)
    {
        foreach (var pid in productIds)
        {
            // Some products intentionally out of stock (0) to exercise availability rules.
            var onHand = (pid % 17 == 0) ? 0 : _rng.Next(5, 500);
            Exec(conn,
                "INSERT dbo.Inventory (ProductId, QuantityOnHand, QuantityAllocated, ReorderLevel) VALUES (@p,@o,0,@r)",
                ("@p", pid), ("@o", onHand), ("@r", _rng.Next(5, 25)));
        }
    }

    // ----------------------------------------------------------------- Customers
    private static readonly string[] First = { "Alice","Bruno","Carla","Diego","Elena","Farid","Grace","Hiro","Ingrid","Jamal","Kara","Liam","Mina","Noah","Olga","Pedro","Qi","Rosa","Sven","Tara","Umar","Vera","Wes","Xena","Yusuf","Zoe" };
    private static readonly string[] Last = { "Nguyen","Smith","Garcia","Kim","Patel","Rossi","Dubois","Haas","Novak","Costa","Khan","Lopez","Meyer","Okafor","Silva","Tanaka","Ivanov","Berg","Reyes","Walsh" };
    private static readonly string[] Cities = { "Springfield","Riverton","Lakeview","Fairview","Georgetown","Franklin","Clinton","Madison","Greenville","Bristol" };
    private static readonly string[] Regions = { "CA","TX","NY","WA","IL","FL","CO","MA","OR","GA" };
    private static readonly string[] Statuses = { "Standard","Standard","Standard","Preferred","Preferred","VIP" };

    private List<int> SeedCustomers(SqlConnection conn)
    {
        var ids = new List<int>();
        for (int i = 1; i <= 120; i++)
        {
            var fn = First[_rng.Next(First.Length)];
            var ln = Last[_rng.Next(Last.Length)];
            var email = $"{fn.ToLowerInvariant()}.{ln.ToLowerInvariant()}{i}@example.test";
            var status = Statuses[_rng.Next(Statuses.Length)];
            // A few customers intentionally missing shipping info (exercise mandatory-info rule).
            var hasAddress = i % 19 != 0;
            ids.Add(ExecScalarInt(conn, @"
INSERT dbo.Customers (FirstName,LastName,Email,Phone,ShipAddressLine,ShipCity,ShipRegion,ShipPostalCode,ShipCountry,CustomerStatus)
OUTPUT INSERTED.CustomerId
VALUES (@fn,@ln,@em,@ph,@addr,@city,@reg,@zip,@ctry,@st)",
                ("@fn", fn), ("@ln", ln), ("@em", email),
                ("@ph", $"555-{_rng.Next(100, 999)}-{_rng.Next(1000, 9999)}"),
                ("@addr", hasAddress ? $"{_rng.Next(1, 9999)} Main St" : (object)DBNull.Value),
                ("@city", hasAddress ? Cities[_rng.Next(Cities.Length)] : (object)DBNull.Value),
                ("@reg", hasAddress ? Regions[_rng.Next(Regions.Length)] : (object)DBNull.Value),
                ("@zip", hasAddress ? $"{_rng.Next(10000, 99999)}" : (object)DBNull.Value),
                ("@ctry", hasAddress ? "USA" : (object)DBNull.Value),
                ("@st", status)));
        }
        return ids;
    }

    private List<int> SeedCustomerAccounts(SqlConnection conn, List<int> customerIds)
    {
        var ids = new List<int>();
        // First 80 customers get portal accounts. Password is "Password123!" for all (dev).
        for (int i = 0; i < 80 && i < customerIds.Count; i++)
        {
            var cid = customerIds[i];
            var username = $"user{cid}";
            var hash = PasswordHasher.HashDeterministic("Password123!", (byte)(cid % 200));
            ids.Add(ExecScalarInt(conn,
                "INSERT dbo.CustomerAccounts (CustomerId, Username, PasswordHash) OUTPUT INSERTED.CustomerAccountId VALUES (@c,@u,@h)",
                ("@c", cid), ("@u", username), ("@h", hash)));
        }
        return ids;
    }

    private void SeedPreferencesAndWishlists(SqlConnection conn, List<int> accountIds, List<int> productIds)
    {
        foreach (var aid in accountIds)
        {
            Exec(conn, "INSERT dbo.CustomerPreferences (CustomerAccountId, PrefKey, PrefValue) VALUES (@a,'Newsletter',@v)",
                ("@a", aid), ("@v", (aid % 2 == 0) ? "true" : "false"));
            var wid = ExecScalarInt(conn,
                "INSERT dbo.Wishlists (CustomerAccountId, Name) OUTPUT INSERTED.WishlistId VALUES (@a,'My Wishlist')", ("@a", aid));
            int items = _rng.Next(0, 4);
            var used = new HashSet<int>();
            for (int k = 0; k < items; k++)
            {
                var pid = productIds[_rng.Next(productIds.Count)];
                if (!used.Add(pid)) continue;
                Exec(conn, "INSERT dbo.WishlistItems (WishlistId, ProductId) VALUES (@w,@p)", ("@w", wid), ("@p", pid));
            }
        }
    }

    // --------------------------------------------------------------- Sales agents
    private List<int> SeedSalesAgents(SqlConnection conn)
    {
        var ids = new List<int>();
        var agents = new (string code, string name, decimal rate, int canOverride)[]
        {
            ("AG-001","Dana Fielding", 0.0300m, 1),
            ("AG-002","Marco Pinto",   0.0250m, 0),
            ("AG-003","Priya Raman",   0.0200m, 1),
            ("AG-004","Tom Becker",    0.0150m, 0),
            ("AG-005","Lena Ortiz",    0.0350m, 1),
        };
        foreach (var a in agents)
            ids.Add(ExecScalarInt(conn, @"
INSERT dbo.SalesAgents (AgentCode, FullName, Email, CommissionRate, CanOverrideCancellation)
OUTPUT INSERTED.SalesAgentId VALUES (@c,@n,@e,@r,@o)",
                ("@c", a.code), ("@n", a.name), ("@e", $"{a.code.ToLowerInvariant()}@retail.test"),
                ("@r", a.rate), ("@o", a.canOverride)));
        return ids;
    }

    // ---------------------------------------------------------------- Warehouses
    private List<int> SeedWarehouses(SqlConnection conn, out List<int> binIds)
    {
        var ids = new List<int>();
        binIds = new List<int>();
        var whs = new (string code, string name, string region)[]
        {
            ("WH-EAST","East Coast DC","East"),
            ("WH-WEST","West Coast DC","West"),
            ("WH-CTRL","Central DC","Central"),
        };
        foreach (var w in whs)
        {
            var wid = ExecScalarInt(conn,
                "INSERT dbo.Warehouses (Code, Name, Region) OUTPUT INSERTED.WarehouseId VALUES (@c,@n,@r)",
                ("@c", w.code), ("@n", w.name), ("@r", w.region));
            ids.Add(wid);
            foreach (var aisle in new[] { "A", "B", "C" })
                for (int b = 1; b <= 4; b++)
                    binIds.Add(ExecScalarInt(conn,
                        "INSERT dbo.WarehouseBins (WarehouseId, BinCode, Aisle) OUTPUT INSERTED.WarehouseBinId VALUES (@w,@b,@a)",
                        ("@w", wid), ("@b", $"{aisle}-{b:00}"), ("@a", aisle)));
        }
        return ids;
    }

    // -------------------------------------------------------------------- Coupons
    private List<int> SeedCoupons(SqlConnection conn)
    {
        var ids = new List<int>();
        var coupons = new (string code, string type, decimal val, decimal min, string? status, int? max)[]
        {
            ("WELCOME10","Percent", 10m, 0m, null, 1000),
            ("VIP20",    "Percent", 20m, 100m, "VIP", 500),
            ("SAVE25",   "FixedAmount", 25m, 150m, null, 300),
            ("FREESHIP", "FixedAmount", 10m, 50m, null, null),
        };
        foreach (var c in coupons)
            ids.Add(ExecScalarInt(conn, @"
INSERT dbo.Coupons (Code, CouponType, Value, MinCartTotal, RequiredStatus, MaxRedemptions, ExpiresUtc, IsActive)
OUTPUT INSERTED.CouponId VALUES (@code,@t,@v,@m,@s,@max,DATEADD(DAY,180,SYSUTCDATETIME()),1)",
                ("@code", c.code), ("@t", c.type), ("@v", c.val), ("@m", c.min),
                ("@s", (object?)c.status ?? DBNull.Value), ("@max", (object?)c.max ?? DBNull.Value)));
        return ids;
    }

    // --------------------------------------------------------------------- Orders
    private static readonly string[] OrderStatuses =
        { "Pending","Confirmed","Confirmed","Allocated","Picking","Packed","Shipped","Delivered","Cancelled" };

    private List<int> SeedOrders(SqlConnection conn, List<int> customerIds, List<int> productIds, List<int> agentIds)
    {
        var ids = new List<int>();
        for (int i = 1; i <= 240; i++)
        {
            var cid = customerIds[_rng.Next(customerIds.Count)];
            var status = OrderStatuses[_rng.Next(OrderStatuses.Length)];
            var source = (i % 2 == 0) ? "CustomerPortal" : "OrderDesk";
            int? agent = source == "OrderDesk" ? agentIds[_rng.Next(agentIds.Count)] : (int?)null;
            var orderNo = $"ORD-{i:000000}";

            var oid = ExecScalarInt(conn, @"
INSERT dbo.Orders (OrderNumber, CustomerId, OrderStatus, OrderSource, SubTotal, DiscountTotal, GrandTotal, PlacedByAgentId, PlacedUtc)
OUTPUT INSERTED.OrderId
VALUES (@no,@c,@st,@src,0,0,0,@ag, DATEADD(DAY,@daysAgo,SYSUTCDATETIME()))",
                ("@no", orderNo), ("@c", cid), ("@st", status), ("@src", source),
                ("@ag", (object?)agent ?? DBNull.Value), ("@daysAgo", -_rng.Next(1, 120)));

            decimal sub = 0, disc = 0;
            int lineCount = _rng.Next(1, 5);
            var usedProducts = new HashSet<int>();
            for (int l = 0; l < lineCount; l++)
            {
                var pid = productIds[_rng.Next(productIds.Count)];
                if (!usedProducts.Add(pid)) continue;
                var qty = _rng.Next(1, 6);
                var unit = GetProductPrice(conn, pid);
                var lineDisc = Math.Round(unit * qty * (decimal)(_rng.NextDouble() * 0.1), 2);
                Exec(conn, "INSERT dbo.OrderLines (OrderId, ProductId, Quantity, UnitPrice, LineDiscount) VALUES (@o,@p,@q,@u,@d)",
                    ("@o", oid), ("@p", pid), ("@q", qty), ("@u", unit), ("@d", lineDisc));
                sub += unit * qty;
                disc += lineDisc;
            }
            Exec(conn, "UPDATE dbo.Orders SET SubTotal=@s, DiscountTotal=@d, GrandTotal=@g WHERE OrderId=@o",
                ("@s", sub), ("@d", disc), ("@g", sub - disc), ("@o", oid));
            ids.Add(oid);
        }
        return ids;
    }

    private readonly Dictionary<int, decimal> _priceCache = new();
    private decimal GetProductPrice(SqlConnection conn, int productId)
    {
        if (_priceCache.TryGetValue(productId, out var p)) return p;
        using var cmd = new SqlCommand("SELECT UnitPrice FROM dbo.Products WHERE ProductId=@p", conn);
        cmd.Parameters.AddWithValue("@p", productId);
        var price = (decimal)cmd.ExecuteScalar()!;
        _priceCache[productId] = price;
        return price;
    }

    // ---------------------------------------------------------------------- Quotes
    private void SeedQuotes(SqlConnection conn, List<int> customerIds, List<int> productIds, List<int> agentIds, List<int> orderIds)
    {
        for (int i = 1; i <= 40; i++)
        {
            var cid = customerIds[_rng.Next(customerIds.Count)];
            var aid = agentIds[_rng.Next(agentIds.Count)];
            var disc = (decimal)_rng.Next(0, 35);
            // Status mix: Draft, PendingApproval, Approved, Rejected, Expired, Converted
            string status;
            var r = i % 6;
            status = r switch
            {
                0 => "Draft",
                1 => "PendingApproval",
                2 => "Approved",
                3 => "Rejected",
                4 => "Expired",
                _ => "Converted"
            };
            int expiresDays = status == "Expired" ? -_rng.Next(1, 20) : _rng.Next(5, 30);

            var qNo = $"Q-{i:00000}";
            var qid = ExecScalarInt(conn, @"
INSERT dbo.SalesQuotes (QuoteNumber, CustomerId, SalesAgentId, QuoteStatus, DiscountPercent, SubTotal, GrandTotal, ExpiresUtc)
OUTPUT INSERTED.SalesQuoteId
VALUES (@no,@c,@a,@st,@disc,0,0, DATEADD(DAY,@exp,SYSUTCDATETIME()))",
                ("@no", qNo), ("@c", cid), ("@a", aid), ("@st", status), ("@disc", disc), ("@exp", expiresDays));

            decimal sub = 0;
            int lines = _rng.Next(1, 4);
            var used = new HashSet<int>();
            for (int l = 0; l < lines; l++)
            {
                var pid = productIds[_rng.Next(productIds.Count)];
                if (!used.Add(pid)) continue;
                var qty = _rng.Next(1, 8);
                var unit = GetProductPrice(conn, pid);
                Exec(conn, "INSERT dbo.SalesQuoteLines (SalesQuoteId, ProductId, Quantity, UnitPrice) VALUES (@q,@p,@qty,@u)",
                    ("@q", qid), ("@p", pid), ("@qty", qty), ("@u", unit));
                sub += unit * qty;
            }
            var grand = Math.Round(sub * (1 - disc / 100m), 2);
            Exec(conn, "UPDATE dbo.SalesQuotes SET SubTotal=@s, GrandTotal=@g WHERE SalesQuoteId=@q",
                ("@s", sub), ("@g", grand), ("@q", qid));

            // Discount approvals for quotes above threshold (15%).
            if (disc > 15)
            {
                string apStatus = status == "Rejected" ? "Rejected" : (status is "Approved" or "Converted" ? "Approved" : "Pending");
                int? approver = apStatus == "Pending" ? (int?)null : aid;
                Exec(conn, @"
INSERT dbo.DiscountApprovals (SalesQuoteId, RequestedPercent, ApprovalStatus, ApprovedByAgentId, DecisionUtc, Notes)
VALUES (@q,@p,@st,@ag,@dt,@notes)",
                    ("@q", qid), ("@p", disc), ("@st", apStatus),
                    ("@ag", (object?)approver ?? DBNull.Value),
                    ("@dt", apStatus == "Pending" ? (object)DBNull.Value : DateTime.UtcNow),
                    ("@notes", $"Discount {disc}% requested"));
            }
        }
    }

    private void SeedCommissionsAndTasks(SqlConnection conn, List<int> agentIds, List<int> customerIds, List<int> orderIds)
    {
        // Commissions for a subset of OrderDesk-sourced orders (unique per order).
        using (var cmd = new SqlCommand(
            "SELECT TOP 40 OrderId, GrandTotal, PlacedByAgentId FROM dbo.Orders WHERE OrderSource='OrderDesk' AND PlacedByAgentId IS NOT NULL ORDER BY OrderId", conn))
        using (var r = cmd.ExecuteReader())
        {
            var rows = new List<(int oid, decimal total, int agent)>();
            while (r.Read()) rows.Add((r.GetInt32(0), r.GetDecimal(1), r.GetInt32(2)));
            r.Close();
            foreach (var row in rows)
            {
                var rate = GetAgentRate(conn, row.agent);
                var amt = Math.Round(row.total * rate, 2);
                Exec(conn, @"
IF NOT EXISTS (SELECT 1 FROM dbo.SalesCommissions WHERE OrderId=@o)
INSERT dbo.SalesCommissions (SalesAgentId, OrderId, OrderValue, DiscountPercent, CommissionAmount)
VALUES (@a,@o,@v,0,@amt)",
                    ("@a", row.agent), ("@o", row.oid), ("@v", row.total), ("@amt", amt));
            }
        }

        // Agent tasks.
        string[] titles = { "Follow up on quote", "Call customer", "Review discount request", "Prepare proposal", "Check order status" };
        for (int i = 0; i < 30; i++)
        {
            var aid = agentIds[_rng.Next(agentIds.Count)];
            var cid = customerIds[_rng.Next(customerIds.Count)];
            var st = new[] { "Open", "Open", "InProgress", "Done" }[_rng.Next(4)];
            Exec(conn, @"
INSERT dbo.SalesAgentTasks (SalesAgentId, Title, Details, RelatedCustomerId, TaskStatus, DueUtc)
VALUES (@a,@t,@d,@c,@st, DATEADD(DAY,@due,SYSUTCDATETIME()))",
                ("@a", aid), ("@t", titles[_rng.Next(titles.Length)]),
                ("@d", "Auto-generated task"), ("@c", cid), ("@st", st), ("@due", _rng.Next(1, 14)));
        }
    }

    private readonly Dictionary<int, decimal> _rateCache = new();
    private decimal GetAgentRate(SqlConnection conn, int agentId)
    {
        if (_rateCache.TryGetValue(agentId, out var r)) return r;
        using var cmd = new SqlCommand("SELECT CommissionRate FROM dbo.SalesAgents WHERE SalesAgentId=@a", conn);
        cmd.Parameters.AddWithValue("@a", agentId);
        var rate = (decimal)cmd.ExecuteScalar()!;
        _rateCache[agentId] = rate;
        return rate;
    }

    // ----------------------------------------------------------------------- Carts
    private void SeedCarts(SqlConnection conn, List<int> accountIds, List<int> productIds, List<int> couponIds)
    {
        foreach (var aid in accountIds)
        {
            // Not every account has a cart.
            if (_rng.Next(0, 10) < 3) continue;
            string status = new[] { "Active", "Active", "CheckedOut", "Abandoned" }[_rng.Next(4)];
            string? coupon = (_rng.Next(0, 4) == 0) ? "WELCOME10" : null;
            var cartId = ExecScalarInt(conn, @"
INSERT dbo.ShoppingCarts (CustomerAccountId, CartStatus, AppliedCouponCode, CheckedOutUtc)
OUTPUT INSERTED.ShoppingCartId
VALUES (@a,@st,@cp,@co)",
                ("@a", aid), ("@st", status), ("@cp", (object?)coupon ?? DBNull.Value),
                ("@co", status == "CheckedOut" ? (object)DateTime.UtcNow : DBNull.Value));

            int items = _rng.Next(1, 5);
            var used = new HashSet<int>();
            foreach (var _ in Enumerable.Range(0, items))
            {
                var pid = productIds[_rng.Next(productIds.Count)];
                if (!used.Add(pid)) continue;
                Exec(conn, "INSERT dbo.ShoppingCartItems (ShoppingCartId, ProductId, Quantity) VALUES (@c,@p,@q)",
                    ("@c", cartId), ("@p", pid), ("@q", _rng.Next(1, 4)));
            }
        }

        // A handful of coupon redemptions tied to CustomerPortal orders.
        using var ocmd = new SqlCommand(
            "SELECT TOP 15 OrderId FROM dbo.Orders WHERE OrderSource='CustomerPortal' ORDER BY OrderId", conn);
        using var orr = ocmd.ExecuteReader();
        var portalOrders = new List<int>();
        while (orr.Read()) portalOrders.Add(orr.GetInt32(0));
        orr.Close();
        int idx = 0;
        foreach (var oid in portalOrders)
        {
            var aid = accountIds[idx % accountIds.Count];
            var coupon = couponIds[idx % couponIds.Count];
            Exec(conn, @"
INSERT dbo.CouponRedemptions (CouponId, CustomerAccountId, OrderId, AmountDiscounted)
VALUES (@c,@a,@o,@amt)",
                ("@c", coupon), ("@a", aid), ("@o", oid), ("@amt", Math.Round((decimal)(_rng.NextDouble() * 30 + 5), 2)));
            idx++;
        }
    }

    // ------------------------------------------------------------------ Fulfillment
    private void SeedFulfillment(SqlConnection conn, List<int> orderIds, List<int> productIds, List<int> warehouseIds, List<int> binIds)
    {
        // For orders in Allocated..Delivered, create allocations/picklists/shipments.
        using var cmd = new SqlCommand(
            "SELECT OrderId, OrderStatus FROM dbo.Orders WHERE OrderStatus IN ('Allocated','Picking','Packed','Shipped','Delivered')", conn);
        using var r = cmd.ExecuteReader();
        var rows = new List<(int oid, string st)>();
        while (r.Read()) rows.Add((r.GetInt32(0), r.GetString(1)));
        r.Close();

        foreach (var (oid, st) in rows)
        {
            var wid = warehouseIds[oid % warehouseIds.Count];
            // Allocations from the order lines.
            var lines = GetOrderLines(conn, oid);
            foreach (var (pid, qty) in lines)
                Exec(conn, @"
INSERT dbo.InventoryAllocations (OrderId, ProductId, WarehouseId, QuantityAllocated, AllocationStatus)
VALUES (@o,@p,@w,@q,'Allocated')",
                    ("@o", oid), ("@p", pid), ("@w", wid), ("@q", qty));

            // Pick list.
            var pickStatus = st == "Allocated" ? "Created" : (st == "Picking" ? "InProgress" : "Completed");
            var plId = ExecScalarInt(conn,
                "INSERT dbo.PickLists (OrderId, WarehouseId, PickStatus) OUTPUT INSERTED.PickListId VALUES (@o,@w,@s)",
                ("@o", oid), ("@w", wid), ("@s", pickStatus));
            foreach (var (pid, qty) in lines)
            {
                var picked = pickStatus == "Completed" ? qty : (pickStatus == "InProgress" ? qty / 2 : 0);
                Exec(conn, @"
INSERT dbo.PickListItems (PickListId, ProductId, WarehouseBinId, QuantityToPick, QuantityPicked, IsPicked)
VALUES (@pl,@p,@bin,@qt,@qp,@done)",
                    ("@pl", plId), ("@p", pid), ("@bin", binIds[_rng.Next(binIds.Count)]),
                    ("@qt", qty), ("@qp", picked), ("@done", picked >= qty ? 1 : 0));
            }

            // Shipment for Packed/Shipped/Delivered.
            if (st is "Packed" or "Shipped" or "Delivered")
            {
                var shipStatus = st == "Packed" ? "Packed" : (st == "Shipped" ? "Dispatched" : "Delivered");
                var carrier = new[] { "UPS", "FedEx", "DHL", "USPS" }[_rng.Next(4)];
                var tracking = $"1Z{_rng.Next(100000, 999999)}{_rng.Next(1000, 9999)}";
                var sid = ExecScalarInt(conn, @"
INSERT dbo.Shipments (OrderId, ShipmentStatus, Carrier, TrackingNumber, DispatchedUtc)
OUTPUT INSERTED.ShipmentId
VALUES (@o,@st,@c,@t,@d)",
                    ("@o", oid), ("@st", shipStatus), ("@c", carrier), ("@t", tracking),
                    ("@d", st is "Shipped" or "Delivered" ? (object)DateTime.UtcNow.AddDays(-_rng.Next(1, 10)) : DBNull.Value));

                Exec(conn, "INSERT dbo.ShipmentPackages (ShipmentId, PackageCode, WeightKg) VALUES (@s,@c,@w)",
                    ("@s", sid), ("@c", $"PKG-{sid:000000}"), ("@w", Math.Round((decimal)(_rng.NextDouble() * 10 + 0.5), 3)));

                // Carrier events (tracking history).
                var baseUtc = DateTime.UtcNow.AddDays(-5);
                Exec(conn, "INSERT dbo.CarrierEvents (ShipmentId, EventCode, Description, EventUtc) VALUES (@s,'DISPATCHED','Left origin facility',@u)",
                    ("@s", sid), ("@u", baseUtc));
                Exec(conn, "INSERT dbo.CarrierEvents (ShipmentId, EventCode, Description, EventUtc) VALUES (@s,'IN_TRANSIT','In transit',@u)",
                    ("@s", sid), ("@u", baseUtc.AddDays(1)));
                if (st == "Delivered")
                    Exec(conn, "INSERT dbo.CarrierEvents (ShipmentId, EventCode, Description, EventUtc) VALUES (@s,'DELIVERED','Delivered to recipient',@u)",
                        ("@s", sid), ("@u", baseUtc.AddDays(2)));
            }
        }
    }

    private List<(int pid, int qty)> GetOrderLines(SqlConnection conn, int orderId)
    {
        var list = new List<(int, int)>();
        using var cmd = new SqlCommand("SELECT ProductId, SUM(Quantity) FROM dbo.OrderLines WHERE OrderId=@o GROUP BY ProductId", conn);
        cmd.Parameters.AddWithValue("@o", orderId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt32(0), r.GetInt32(1)));
        return list;
    }

    // ---------------------------------------------------------------------- Returns
    private void SeedReturns(SqlConnection conn, List<int> orderIds, List<int> customerIds, List<int> productIds, List<int> warehouseIds)
    {
        // Returns only against Delivered orders.
        using var cmd = new SqlCommand(
            "SELECT TOP 25 OrderId, CustomerId FROM dbo.Orders WHERE OrderStatus='Delivered' ORDER BY OrderId", conn);
        using var r = cmd.ExecuteReader();
        var rows = new List<(int oid, int cid)>();
        while (r.Read()) rows.Add((r.GetInt32(0), r.GetInt32(1)));
        r.Close();

        int i = 0;
        foreach (var (oid, cid) in rows)
        {
            i++;
            // Status mix across the cross-app return lifecycle.
            string status = (i % 5) switch
            {
                0 => "Requested",
                1 => "Approved",
                2 => "Rejected",
                3 => "Received",
                _ => "Closed"
            };
            var rNo = $"RET-{i:00000}";
            int? reviewer = status is "Approved" or "Rejected" or "Received" or "Closed" ? 1 : (int?)null;
            var retId = ExecScalarInt(conn, @"
INSERT dbo.Returns (ReturnNumber, OrderId, CustomerId, ReturnStatus, Reason, ReviewedByAgentId, ReviewedUtc)
OUTPUT INSERTED.ReturnId
VALUES (@no,@o,@c,@st,@reason,@rev,@revUtc)",
                ("@no", rNo), ("@o", oid), ("@c", cid), ("@st", status),
                ("@reason", "Item not as expected"),
                ("@rev", (object?)reviewer ?? DBNull.Value),
                ("@revUtc", reviewer is null ? (object)DBNull.Value : DateTime.UtcNow));

            var lines = GetOrderLines(conn, oid);
            foreach (var (pid, qty) in lines.Take(2))
                Exec(conn, "INSERT dbo.ReturnLines (ReturnId, ProductId, Quantity) VALUES (@r,@p,@q)",
                    ("@r", retId), ("@p", pid), ("@q", Math.Max(1, qty / 2)));

            // Received/Closed returns have a warehouse receipt.
            if (status is "Received" or "Closed")
            {
                var wid = warehouseIds[oid % warehouseIds.Count];
                var rrId = ExecScalarInt(conn,
                    "INSERT dbo.ReturnReceipts (ReturnId, WarehouseId, ReceivedStatus) OUTPUT INSERTED.ReturnReceiptId VALUES (@r,@w,'Restocked')",
                    ("@r", retId), ("@w", wid));
                foreach (var (pid, qty) in lines.Take(2))
                    Exec(conn, @"
INSERT dbo.WarehouseAdjustments (ProductId, WarehouseId, QuantityDelta, Reason, RelatedReturnReceiptId)
VALUES (@p,@w,@q,'Return restock',@rr)",
                        ("@p", pid), ("@w", wid), ("@q", Math.Max(1, qty / 2)), ("@rr", rrId));
            }
        }
    }

    // --------------------------------------------------------------- Support tickets
    private void SeedSupportTickets(SqlConnection conn, List<int> accountIds, List<int> orderIds)
    {
        string[] subjects = { "Where is my order?", "Wrong item received", "Refund question", "Account login issue", "Product inquiry" };
        for (int i = 0; i < 35; i++)
        {
            var aid = accountIds[_rng.Next(accountIds.Count)];
            var st = new[] { "Open", "Open", "Pending", "Resolved", "Closed" }[_rng.Next(5)];
            var tid = ExecScalarInt(conn, @"
INSERT dbo.SupportTickets (CustomerAccountId, Subject, TicketStatus)
OUTPUT INSERTED.SupportTicketId VALUES (@a,@s,@st)",
                ("@a", aid), ("@s", subjects[_rng.Next(subjects.Length)]), ("@st", st));
            Exec(conn, "INSERT dbo.SupportTicketMessages (SupportTicketId, Author, Body) VALUES (@t,'Customer',@b)",
                ("@t", tid), ("@b", "Please help with my issue."));
            if (st is "Resolved" or "Closed")
                Exec(conn, "INSERT dbo.SupportTicketMessages (SupportTicketId, Author, Body) VALUES (@t,'Support',@b)",
                    ("@t", tid), ("@b", "Resolved. Thank you for your patience."));
        }
    }
}
