using FulfillmentAPI.Application;
using Xunit;

namespace IntegrationTests;

[Collection("Database")]
public class FulfillmentWorkflowTests
{
    private readonly DbFixture _fx;
    public FulfillmentWorkflowTests(DbFixture fx) => _fx = fx;

    [Fact]
    public async Task Allocation_reserves_stock_and_blocks_over_allocation()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, stock: 5);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 3);

        using var db = _fx.Fulfillment();
        var alloc = new AllocationService(db);

        var result = await alloc.AllocateAsync(orderId, warehouseId);
        Assert.True(result.Success, result.Error);

        var (onHand, allocated) = td.GetInventory(productId);
        Assert.Equal(5, onHand);
        Assert.Equal(3, allocated);

        // Second order needs 3 more but only 2 available -> blocked.
        var order2 = td.CreateOrder(customerId, productId, "Confirmed", qty: 3);
        using var db2 = _fx.Fulfillment();
        var over = await new AllocationService(db2).AllocateAsync(order2, warehouseId);
        Assert.False(over.Success);
        Assert.Equal(ApiErrorKind.Conflict, over.ErrorKind);
    }

    [Fact]
    public async Task Allocation_blocked_for_cancelled_order()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Cancelled");

        using var db = _fx.Fulfillment();
        var result = await new AllocationService(db).AllocateAsync(orderId, warehouseId);
        Assert.False(result.Success);
        Assert.Equal(ApiErrorKind.Conflict, result.ErrorKind);
    }

    [Fact]
    public async Task Picking_requires_prior_allocation()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 2);

        using var db = _fx.Fulfillment();
        var picking = new PickingService(db);

        // No allocation yet -> pick list creation blocked.
        var blocked = await picking.CreatePickListAsync(orderId, warehouseId);
        Assert.False(blocked.Success);
        Assert.Equal(ApiErrorKind.Conflict, blocked.ErrorKind);

        // Allocate, then pick list succeeds.
        using var db2 = _fx.Fulfillment();
        Assert.True((await new AllocationService(db2).AllocateAsync(orderId, warehouseId)).Success);

        using var db3 = _fx.Fulfillment();
        var ok = await new PickingService(db3).CreatePickListAsync(orderId, warehouseId);
        Assert.True(ok.Success, ok.Error);
        Assert.NotEmpty(ok.Value!.Items);
    }

    [Fact]
    public async Task Full_warehouse_flow_allocate_pick_package_dispatch()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 2);

        using (var db = _fx.Fulfillment())
            Assert.True((await new AllocationService(db).AllocateAsync(orderId, warehouseId)).Success);

        int pickListId;
        using (var db = _fx.Fulfillment())
        {
            var pl = await new PickingService(db).CreatePickListAsync(orderId, warehouseId);
            Assert.True(pl.Success);
            pickListId = pl.Value!.PickListId;
        }

        // Pick all items.
        using (var db = _fx.Fulfillment())
        {
            var picking = new PickingService(db);
            var list = await picking.GetPickListAsync(pickListId);
            foreach (var item in list!.Items)
                Assert.True((await picking.MarkItemPickedAsync(item.PickListItemId, item.QuantityToPick)).Success);
        }

        int shipmentId;
        using (var db = _fx.Fulfillment())
        {
            var shipments = new ShipmentService(db);
            var created = await shipments.CreateShipmentWithPackageAsync(orderId, "PKG-IT-" + Guid.NewGuid().ToString("N")[..8], 1.5m);
            Assert.True(created.Success, created.Error);
            shipmentId = created.Value!.ShipmentId;
        }

        using (var db = _fx.Fulfillment())
        {
            var shipments = new ShipmentService(db);

            // Dispatch without carrier -> conflict.
            var noCarrier = await shipments.UpdateStatusAsync(shipmentId, "Dispatched", null, null);
            Assert.False(noCarrier.Success);
            Assert.Equal(ApiErrorKind.Conflict, noCarrier.ErrorKind);

            // Dispatch with carrier -> success.
            var dispatched = await shipments.UpdateStatusAsync(shipmentId, "Dispatched", "UPS", "1Z-IT-123");
            Assert.True(dispatched.Success, dispatched.Error);
        }

        Assert.Equal("Shipped", td.GetOrderStatus(orderId));
    }

    [Fact]
    public async Task Carrier_events_are_idempotent()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 1);

        using (var db = _fx.Fulfillment())
            await new AllocationService(db).AllocateAsync(orderId, warehouseId);
        int pickListId;
        using (var db = _fx.Fulfillment())
            pickListId = (await new PickingService(db).CreatePickListAsync(orderId, warehouseId)).Value!.PickListId;
        using (var db = _fx.Fulfillment())
        {
            var picking = new PickingService(db);
            foreach (var i in (await picking.GetPickListAsync(pickListId))!.Items)
                await picking.MarkItemPickedAsync(i.PickListItemId, i.QuantityToPick);
        }
        int shipmentId;
        using (var db = _fx.Fulfillment())
            shipmentId = (await new ShipmentService(db).CreateShipmentWithPackageAsync(orderId, "PKG-IT-" + Guid.NewGuid().ToString("N")[..8], 1m)).Value!.ShipmentId;

        var when = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        using var db1 = _fx.Fulfillment();
        var svc = new ShipmentService(db1);
        var e1 = await svc.RecordCarrierEventAsync(shipmentId, "IN_TRANSIT", "x", when);
        var e2 = await svc.RecordCarrierEventAsync(shipmentId, "IN_TRANSIT", "x", when);
        Assert.True(e1.Success && e2.Success);
        Assert.Equal(e1.Value!.CarrierEventId, e2.Value!.CarrierEventId); // same row, no duplicate
    }

    [Fact]
    public async Task Return_receipt_restocks_inventory_and_is_idempotent_and_requires_approval()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, stock: 10);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Delivered", qty: 4);

        // A 'Requested' (not approved) return cannot be received.
        var requested = td.CreateReturn(orderId, customerId, productId, "Requested", qty: 2);
        using (var db = _fx.Fulfillment())
        {
            var notApproved = await new ReturnReceiptService(db).ReceiveAsync(requested, warehouseId);
            Assert.False(notApproved.Success);
            Assert.Equal(ApiErrorKind.Conflict, notApproved.ErrorKind);
        }

        // An 'Approved' return is received: inventory restocked, idempotent.
        var approved = td.CreateReturn(orderId, customerId, productId, "Approved", qty: 2);
        var (onHandBefore, _) = td.GetInventory(productId);

        int receiptId;
        using (var db = _fx.Fulfillment())
        {
            var r1 = await new ReturnReceiptService(db).ReceiveAsync(approved, warehouseId);
            Assert.True(r1.Success, r1.Error);
            receiptId = r1.Value!.ReturnReceiptId;
        }

        var (onHandAfter, _) = td.GetInventory(productId);
        Assert.Equal(onHandBefore + 2, onHandAfter); // restocked

        // Return status moved to 'Received' (NOT refunded).
        Assert.Equal("Received", td.GetReturnStatus(approved));

        // Idempotent: receiving again returns the same receipt and does not double-restock.
        using (var db = _fx.Fulfillment())
        {
            var r2 = await new ReturnReceiptService(db).ReceiveAsync(approved, warehouseId);
            Assert.True(r2.Success);
            Assert.Equal(receiptId, r2.Value!.ReturnReceiptId);
        }
        var (onHandFinal, _) = td.GetInventory(productId);
        Assert.Equal(onHandAfter, onHandFinal); // no double restock
    }
}
