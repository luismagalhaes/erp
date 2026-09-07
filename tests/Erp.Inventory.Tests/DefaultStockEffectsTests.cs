using Erp.Sales.Domain;
using FluentAssertions;

namespace Erp.Inventory.Tests;

/// <summary>
/// What each document type does to stock by default. These are business defaults, not law, so the
/// point of pinning them is that a change is deliberate rather than accidental.
/// </summary>
public class DefaultStockEffectsTests
{
    [Theory]
    [InlineData("GR")]
    [InlineData("GT")]
    [InlineData("GC")]
    public void Goods_leaving_on_a_movement_document_take_stock_out(string documentType)
    {
        DefaultStockEffects.For(documentType).Should().Be(StockEffect.Out);
    }

    [Fact]
    public void A_return_note_brings_stock_back_in()
    {
        DefaultStockEffects.For("GD").Should().Be(StockEffect.In);
    }

    /// <summary>Own assets move between the entity's own places: nothing is bought or sold.</summary>
    [Fact]
    public void An_own_assets_note_moves_no_stock()
    {
        DefaultStockEffects.For("GA").Should().Be(StockEffect.None);
    }

    [Theory]
    [InlineData("FT")]
    [InlineData("FS")]
    [InlineData("FR")]
    public void A_sale_takes_stock_out(string documentType)
    {
        DefaultStockEffects.For(documentType).Should().Be(StockEffect.Out);
    }

    [Fact]
    public void A_credit_note_brings_stock_back_in()
    {
        DefaultStockEffects.For("NC").Should().Be(StockEffect.In);
    }

    /// <summary>A debit note corrects value, never quantity; receipts move money, not goods.</summary>
    [Theory]
    [InlineData("ND")]
    [InlineData("RC")]
    [InlineData("RG")]
    public void Documents_that_only_move_value_leave_stock_alone(string documentType)
    {
        DefaultStockEffects.For(documentType).Should().Be(StockEffect.None);
    }
}
