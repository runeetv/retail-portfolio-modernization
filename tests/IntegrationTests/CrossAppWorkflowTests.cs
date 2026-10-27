using CustomerPortal.Application;
using FulfillmentAPI.Application;
using OrderDesk.Application;
using OrderDesk.Infrastructure;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Cross-application workflows: a single business process spanning OrderDesk,
/// CustomerPortal, and FulfillmentAPI against the shared database.
/// </summary>
[Collection("Database")]
public class CrossAppWorkflowTests
{
    private readonly DbFixture _fx;
    public CrossAppWorkflowTests(DbFixture fx) => _fx = fx;

    private Func<OrderDeskDbContext> OdCtx => () => _fx.OrderDesk();

    [Fact]
    public async Task WorkflowC_customer_return_orderdesk_approve_warehouse_receive()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, stock: 10);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Delivered", qty: 3);

        // 1. CustomerPortal: customer requests a return.
        int returnId;
        using (var portal = _fx.Portal())
        {
            var result = await new ReturnService(portal).RequestReturnAsync(customerId, orderId, "defective");
            Assert.True(result.Success, result.Error);
            returnId = result.Value;
        }
        Assert.Equal("Requested", td.GetReturnStatus(returnId));

        // 2. OrderDesk: staff approves the return.
        var agentId = td.SalesAgent();
        var review = await new ReturnReviewService(OdCtx).ReviewAsync(returnId, approve: true, agentId: agentId);
        Assert.True(review.Success, review.Error);
        Assert.Equal("Approved", td.GetReturnStatus(returnId));

        // 3. FulfillmentAPI: warehouse receives the approved return (restock + status Received).
        var (onHandBefore, _) = td.GetInventory(productId);
        using (var fulfil = _fx.Fulfillment())
        {
            var receipt = await new ReturnReceiptService(fulfil).ReceiveAsync(returnId, warehouseId);
            Assert.True(receipt.Success, receipt.Error);
        }
        Assert.Equal("Received", td.GetReturnStatus(returnId));
        var (onHandAfter, _) = td.GetInventory(productId);
        Assert.True(onHandAfter > onHandBefore);
    }

    [Fact]
    public async Task WorkflowD_customer_cancel_blocks_fulfillment_allocation()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 2);

        // 1. CustomerPortal: customer cancels an eligible (Confirmed) order.
        using (var portal = _fx.Portal())
        {
            var cancel = await new OrderHistoryService(portal).RequestCancellationAsync(customerId, orderId, "changed mind");
            Assert.True(cancel.Success, cancel.Error);
        }
        Assert.Equal("Cancelled", td.GetOrderStatus(orderId));

        // 2. FulfillmentAPI: allocation is rejected for the cancelled order.
        using (var fulfil = _fx.Fulfillment())
        {
            var alloc = await new AllocationService(fulfil).AllocateAsync(orderId, warehouseId);
            Assert.False(alloc.Success);
            Assert.Equal(ApiErrorKind.Conflict, alloc.ErrorKind);
        }
    }

    [Fact]
    public async Task WorkflowA_staff_quote_to_order_then_warehouse_fulfillment()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var agentId = td.SalesAgent();
        var productId = td.CreateProduct(100m, stock: 50);
        var warehouseId = td.Warehouse();

        // 1. OrderDesk: quote -> approve discount (auto, below threshold) -> convert to order.
        var quotes = new QuoteService(OdCtx);
        var qid = (await quotes.CreateQuoteAsync(customerId, agentId)).Value!.SalesQuoteId;
        await quotes.AddLineAsync(qid, productId, 2);
        await quotes.ApplyDiscountAsync(qid, 5m);
        var conv = await quotes.ConvertToOrderAsync(qid);
        Assert.True(conv.Success, conv.Error);
        var orderId = conv.Value;

        // 2. FulfillmentAPI: allocate -> pick -> package -> dispatch.
        using (var fulfil = _fx.Fulfillment())
            Assert.True((await new AllocationService(fulfil).AllocateAsync(orderId, warehouseId)).Success);

        int pickListId;
        using (var fulfil = _fx.Fulfillment())
            pickListId = (await new PickingService(fulfil).CreatePickListAsync(orderId, warehouseId)).Value!.PickListId;

        using (var fulfil = _fx.Fulfillment())
        {
            var picking = new PickingService(fulfil);
            foreach (var i in (await picking.GetPickListAsync(pickListId))!.Items)
                await picking.MarkItemPickedAsync(i.PickListItemId, i.QuantityToPick);
        }

        int shipmentId;
        using (var fulfil = _fx.Fulfillment())
            shipmentId = (await new ShipmentService(fulfil).CreateShipmentWithPackageAsync(orderId, "PKG-WA-" + Guid.NewGuid().ToString("N")[..8], 2m)).Value!.ShipmentId;

        using (var fulfil = _fx.Fulfillment())
            Assert.True((await new ShipmentService(fulfil).UpdateStatusAsync(shipmentId, "Dispatched", "FedEx", "FX-WA-1")).Success);

        Assert.Equal("Shipped", td.GetOrderStatus(orderId));
    }

    [Fact]
    public async Task Cross_app_shipment_visibility_portal_and_orderdesk_see_fulfillment_data()
    {
        Assert.True(_fx.DatabaseAvailable);
        using var conn = _fx.OpenConnection();
        var td = new TestData(conn);
        var customerId = td.CreateCustomer(true);
        var productId = td.CreateProduct(50m, 50);
        var warehouseId = td.Warehouse();
        var orderId = td.CreateOrder(customerId, productId, "Confirmed", qty: 1);

        // FulfillmentAPI owns/creates the shipment + tracking.
        using (var fulfil = _fx.Fulfillment())
            await new AllocationService(fulfil).AllocateAsync(orderId, warehouseId);
        int pickListId;
        using (var fulfil = _fx.Fulfillment())
            pickListId = (await new PickingService(fulfil).CreatePickListAsync(orderId, warehouseId)).Value!.PickListId;
        using (var fulfil = _fx.Fulfillment())
        {
            var picking = new PickingService(fulfil);
            foreach (var i in (await picking.GetPickListAsync(pickListId))!.Items)
                await picking.MarkItemPickedAsync(i.PickListItemId, i.QuantityToPick);
        }
        int shipmentId;
        using (var fulfil = _fx.Fulfillment())
            shipmentId = (await new ShipmentService(fulfil).CreateShipmentWithPackageAsync(orderId, "PKG-VIS-" + Guid.NewGuid().ToString("N")[..8], 1m)).Value!.ShipmentId;
        using (var fulfil = _fx.Fulfillment())
        {
            await new ShipmentService(fulfil).UpdateStatusAsync(shipmentId, "Dispatched", "DHL", "DHL-VIS-1");
            await new ShipmentService(fulfil).RecordCarrierEventAsync(shipmentId, "IN_TRANSIT", "moving", DateTime.UtcNow);
        }

        // CustomerPortal reads the tracking (customer-friendly view).
        using (var portal = _fx.Portal())
        {
            var tracking = await new TrackingService(portal).GetOrderTrackingAsync(customerId, orderId);
            Assert.NotEmpty(tracking);
            Assert.Contains(tracking, s => s.TrackingNumber == "DHL-VIS-1");
        }

        // OrderDesk reads shipment status for customer service.
        var shipments = await new OrderService(OdCtx).GetShipmentsAsync(orderId);
        Assert.Contains(shipments, s => s.TrackingNumber == "DHL-VIS-1");
    }
}
