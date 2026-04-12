using dCMS.Core.Exceptions;
using dCMS.Inventory.Models;
using FluentAssertions;

namespace dCMS.Tests.Unit.Inventory.Models;

public sealed class VariantStockTests
{
    private static VariantStock MakeStock(int qty, int reserved = 0) =>
        VariantStock.Restore(1, "var_1", "wh_1", qty, reserved, 1L);

    [Fact]
    public void AvailableQuantity_IsQuantityMinusReserved() => MakeStock(50, 10).AvailableQuantity.Should().Be(40);

    [Fact]
    public void Reserve_WithSufficientStock_IncrementsReserved()
    {
        var stock = MakeStock(50, 10);
        stock.Reserve(5);
        stock.ReservedQuantity.Should().Be(15);
    }

    [Fact]
    public void Reserve_ExceedsAvailable_ThrowsOutOfStockException()
    {
        var stock = MakeStock(10, 8);
        var act = () => stock.Reserve(3);
        act.Should().Throw<OutOfStockException>().Which.Available.Should().Be(2);
    }

    [Fact]
    public void Release_DecrementsReserved()
    {
        var stock = MakeStock(50, 10);
        stock.Release(4);
        stock.ReservedQuantity.Should().Be(6);
    }

    [Fact]
    public void Release_MoreThanReserved_ClampsToZero()
    {
        var stock = MakeStock(50, 5);
        stock.Release(10);
        stock.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public void Adjust_PositiveDelta_IncreasesQuantity()
    {
        var stock = MakeStock(50);
        stock.Adjust(20);
        stock.Quantity.Should().Be(70);
    }

    [Fact]
    public void Adjust_WouldMakeQuantityLessThanReserved_ThrowsStockInvariantException()
    {
        var stock = MakeStock(50, 40);
        var act = () => stock.Adjust(-20);
        act.Should().Throw<StockInvariantException>();
    }

    [Fact]
    public void Adjust_ToExactlyReservedAmount_IsAllowed()
    {
        var stock = MakeStock(50, 40);
        stock.Adjust(-10);
        stock.Quantity.Should().Be(40);
    }
}
