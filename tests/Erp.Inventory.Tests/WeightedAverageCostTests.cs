using Erp.Inventory.Domain;
using FluentAssertions;

namespace Erp.Inventory.Tests;

/// <summary>
/// The costing arithmetic on its own. Everything else in the module leans on it, so it is worth
/// pinning down here rather than only through the services.
/// </summary>
public class WeightedAverageCostTests
{
    /// <summary>Ten at 10 and ten at 20 leave twenty at 15 — not at 20, the last price paid.</summary>
    [Fact]
    public void Buying_twice_averages_the_two_prices_by_quantity()
    {
        var costing = WeightedAverageCost.Empty
            .Apply(10m, 10m)
            .Apply(10m, 20m);

        costing.Quantity.Should().Be(20m);
        costing.Value.Should().Be(300m);
        costing.AverageCost.Should().Be(15m);
    }

    /// <summary>The average is weighted, so a large cheap lot outweighs a small dear one.</summary>
    [Fact]
    public void The_average_follows_the_quantities_not_the_number_of_purchases()
    {
        var costing = WeightedAverageCost.Empty
            .Apply(90m, 10m)
            .Apply(10m, 100m);

        costing.AverageCost.Should().Be(19m);
    }

    /// <summary>
    /// A sale takes stock out at what it was worth. It changes what is left, never what a unit of
    /// it costs — that is the definition of the method.
    /// </summary>
    [Fact]
    public void Selling_leaves_the_average_where_it_was()
    {
        var costing = WeightedAverageCost.Empty
            .Apply(10m, 10m)
            .Apply(10m, 20m);

        var afterSale = costing.Apply(-5m, costing.CostOf(null));

        afterSale.Quantity.Should().Be(15m);
        afterSale.AverageCost.Should().Be(15m);
        afterSale.Value.Should().Be(225m);
    }

    /// <summary>
    /// A movement with no cost of its own takes the average. A purchase brings its own price and
    /// keeps it, which is the whole rule in one test.
    /// </summary>
    [Fact]
    public void A_movement_costs_what_it_says_or_the_average_when_it_says_nothing()
    {
        var costing = new WeightedAverageCost(10m, 150m, 15m);

        costing.CostOf(null).Should().Be(15m);
        costing.CostOf(7m).Should().Be(7m);
    }

    /// <summary>
    /// The classic trap. Buy ten at 5, then ten at 7 — the average is 6. Undo the first purchase
    /// and, if the reversal left at the average, ten units bought at 7 would be left worth 60.
    /// Reversing at the original cost is what keeps it honest.
    /// </summary>
    [Fact]
    public void Reversing_a_purchase_at_its_own_cost_undoes_exactly_what_it_did()
    {
        var costing = WeightedAverageCost.Empty
            .Apply(10m, 5m)
            .Apply(10m, 7m);

        costing.AverageCost.Should().Be(6m);

        var afterReversal = costing.Apply(-10m, 5m);

        afterReversal.Quantity.Should().Be(10m);
        afterReversal.Value.Should().Be(70m);
        afterReversal.AverageCost.Should().Be(7m);
    }

    /// <summary>
    /// Selling everything leaves nothing worth nothing. The average stays as the last thing known,
    /// so the next movement out has something to leave at.
    /// </summary>
    [Fact]
    public void Emptying_the_warehouse_leaves_no_value_but_keeps_the_last_average()
    {
        var costing = WeightedAverageCost.Empty.Apply(10m, 12m);

        var emptied = costing.Apply(-10m, costing.CostOf(null));

        emptied.Quantity.Should().Be(0m);
        emptied.Value.Should().Be(0m);
        emptied.AverageCost.Should().Be(12m);
    }

    /// <summary>
    /// Stock below zero is left visible, but the value follows the quantity: a negative quantity
    /// beside a positive value would read as a second, separate fault.
    /// </summary>
    [Fact]
    public void Going_below_zero_keeps_the_value_and_the_quantity_agreeing()
    {
        var costing = WeightedAverageCost.Empty.Apply(5m, 10m);

        var negative = costing.Apply(-8m, costing.CostOf(null));

        negative.Quantity.Should().Be(-3m);
        negative.AverageCost.Should().Be(10m);
        negative.Value.Should().Be(-30m);
    }

    /// <summary>
    /// Stock that arrives with no cost anywhere is worth nothing, and says so — it does not borrow
    /// a price from somewhere else.
    /// </summary>
    [Fact]
    public void Stock_that_never_had_a_cost_is_worth_nothing()
    {
        var costing = WeightedAverageCost.Empty;

        var applied = costing.Apply(10m, costing.CostOf(null));

        applied.Quantity.Should().Be(10m);
        applied.Value.Should().Be(0m);
        applied.AverageCost.Should().Be(0m);
    }

    /// <summary>
    /// A price that does not divide evenly is kept to six decimals rather than rounded to cents:
    /// rounding the average and multiplying it back out loses money one movement at a time.
    /// </summary>
    [Fact]
    public void The_average_keeps_enough_decimals_to_survive_being_multiplied_back_out()
    {
        var costing = WeightedAverageCost.Empty
            .Apply(3m, 10m)
            .Apply(1m, 0m);

        costing.AverageCost.Should().Be(7.5m);
        costing.Value.Should().Be(30m);
    }

    [Fact]
    public void An_empty_costing_holds_nothing_and_is_worth_nothing()
    {
        WeightedAverageCost.Empty.Quantity.Should().Be(0m);
        WeightedAverageCost.Empty.Value.Should().Be(0m);
        WeightedAverageCost.Empty.AverageCost.Should().Be(0m);
    }
}
