using Erp.Inventory.Domain;
using FluentAssertions;

namespace Erp.Inventory.Tests;

public class StockLedgerEntryTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static StockLedgerEntry Document(StockDirection direction, decimal quantity = 5m) =>
        StockLedgerEntry.FromDocument(
            CompanyId, WarehouseId, "ART001", "Artigo", direction, quantity,
            new DateOnly(2026, 1, 15), "GR", "GR A2026/1", Guid.NewGuid(), Guid.NewGuid());

    /// <summary>
    /// The sign lives on the entry, not on the direction, so a balance is a plain sum with nothing
    /// to get wrong at the point of reading.
    /// </summary>
    [Fact]
    public void FromDocument_signs_an_out_as_negative()
    {
        Document(StockDirection.Out).Quantity.Should().Be(-5m);
    }

    [Fact]
    public void FromDocument_signs_an_in_as_positive()
    {
        Document(StockDirection.In).Quantity.Should().Be(5m);
    }

    [Fact]
    public void FromDocument_keeps_the_line_that_moved_the_stock()
    {
        var lineId = Guid.NewGuid();

        var entry = StockLedgerEntry.FromDocument(
            CompanyId, WarehouseId, "ART001", "Artigo", StockDirection.Out, 1m,
            new DateOnly(2026, 1, 15), "GR", "GR A2026/1", Guid.NewGuid(), lineId);

        entry.SourceLineId.Should().Be(lineId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromDocument_rejects_a_quantity_that_is_not_positive(decimal quantity)
    {
        var act = () => Document(StockDirection.In, quantity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>A document moves stock one way or the other; corrections come from a count.</summary>
    [Fact]
    public void FromDocument_rejects_an_adjustment()
    {
        var act = () => Document(StockDirection.Adjustment);

        act.Should().Throw<ArgumentException>().WithMessage("*never an adjustment*");
    }

    [Fact]
    public void FromAdjustment_keeps_the_sign_of_the_difference()
    {
        var entry = StockLedgerEntry.FromAdjustment(
            CompanyId, WarehouseId, "ART001", "Artigo", -3m, new DateOnly(2026, 1, 15), "Quebra");

        entry.Quantity.Should().Be(-3m);
        entry.Direction.Should().Be(StockDirection.Adjustment);
        entry.Reason.Should().Be("Quebra");
    }

    [Fact]
    public void FromAdjustment_refuses_a_difference_of_zero()
    {
        var act = () => StockLedgerEntry.FromAdjustment(
            CompanyId, WarehouseId, "ART001", "Artigo", 0m, new DateOnly(2026, 1, 15), "Nada");

        act.Should().Throw<ArgumentException>().WithMessage("*changes nothing*");
    }

    [Fact]
    public void FromAdjustment_requires_a_reason()
    {
        var act = () => StockLedgerEntry.FromAdjustment(
            CompanyId, WarehouseId, "ART001", "Artigo", 1m, new DateOnly(2026, 1, 15), "   ");

        act.Should().Throw<ArgumentException>();
    }
}
