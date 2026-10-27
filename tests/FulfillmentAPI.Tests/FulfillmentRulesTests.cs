using FulfillmentAPI.Domain;
using Xunit;

namespace FulfillmentAPI.Tests;

public class FulfillmentRulesTests
{
    [Theory]
    [InlineData("Cancelled", true)]
    [InlineData("Confirmed", false)]
    [InlineData("Allocated", false)]
    public void IsBlockedForFulfillment_only_cancelled(string status, bool expected)
        => Assert.Equal(expected, FulfillmentRules.IsBlockedForFulfillment(status));

    [Theory]
    [InlineData("Allocated", true)]
    [InlineData("Picking", true)]
    [InlineData("Confirmed", false)]  // picking cannot begin before allocation
    public void CanCreatePickList_requires_allocated(string status, bool expected)
        => Assert.Equal(expected, FulfillmentRules.CanCreatePickList(status));

    [Fact]
    public void CanPackageShipment_requires_all_items_picked()
    {
        Assert.True(FulfillmentRules.CanPackageShipment(true));
        Assert.False(FulfillmentRules.CanPackageShipment(false));
    }

    [Theory]
    [InlineData(true, "UPS", "1Z123", true)]
    [InlineData(false, "UPS", "1Z123", false)]  // no package
    [InlineData(true, null, "1Z123", false)]    // no carrier
    [InlineData(true, "UPS", null, false)]      // no tracking
    public void CanDispatch_requires_package_and_carrier_reference(bool hasPackage, string? carrier, string? tracking, bool expected)
        => Assert.Equal(expected, FulfillmentRules.CanDispatch(hasPackage, carrier, tracking));

    [Theory]
    [InlineData("Created", "Packed", true)]
    [InlineData("Packed", "Dispatched", true)]
    [InlineData("Dispatched", "InTransit", true)]
    [InlineData("InTransit", "Delivered", true)]
    [InlineData("Created", "Delivered", false)]  // cannot skip
    [InlineData("Delivered", "Packed", false)]   // cannot go backward
    [InlineData("Created", "Exception", true)]   // exception allowed from any state
    public void IsValidShipmentTransition(string from, string to, bool expected)
        => Assert.Equal(expected, FulfillmentRules.IsValidShipmentTransition(from, to));
}
