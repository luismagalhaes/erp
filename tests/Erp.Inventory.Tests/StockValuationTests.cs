using Erp.Inventory.Domain;
using FluentAssertions;

namespace Erp.Inventory.Tests;

/// <summary>
/// Replaying the ledger. The point being checked throughout is that the replay and the running
/// balance are the same arithmetic — if they were not, the stock check would be comparing two
/// different ideas of what stock is worth and always finding them equal.
/// </summary>
public class StockValuationTests
{
    private readonly Guid _warehouseA = Guid.NewGuid();
    private readonly Guid _warehouseB = Guid.NewGuid();

    private LedgerMovement Movement(
        decimal quantity,
        decimal? unitCost = null,
        string productCode = "ART001",
        Guid? warehouseId = null,
        string description = "Artigo de teste") =>
        new(warehouseId ?? _warehouseA, productCode, description, quantity, unitCost);

    [Fact]
    public void Replay_averages_the_purchases_of_a_product()
    {
        var lines = StockValuation.Replay([Movement(10m, 10m), Movement(10m, 20m)]);

        var line = lines.Should().ContainSingle().Subject;
        line.Quantity.Should().Be(20m);
        line.Value.Should().Be(300m);
        line.AverageCost.Should().Be(15m);
        line.MovementCount.Should().Be(2);
    }

    /// <summary>
    /// The average depends on the order the movements arrived in, not merely their sum. Selling
    /// before the dear purchase leaves a different average than selling after it.
    /// </summary>
    [Fact]
    public void Replay_depends_on_the_order_the_movements_happened_in()
    {
        var sellFirst = StockValuation.Replay([Movement(10m, 10m), Movement(-5m), Movement(10m, 20m)]);
        var sellLast = StockValuation.Replay([Movement(10m, 10m), Movement(10m, 20m), Movement(-5m)]);

        sellFirst.Single().Quantity.Should().Be(sellLast.Single().Quantity);
        sellFirst.Single().AverageCost.Should().Be(16.666667m);
        sellLast.Single().AverageCost.Should().Be(15m);
    }

    /// <summary>
    /// A warehouse is a separate pile of goods with its own history, so it has its own average.
    /// </summary>
    [Fact]
    public void Replay_keeps_each_warehouse_apart()
    {
        var lines = StockValuation.Replay(
        [
            Movement(10m, 10m, warehouseId: _warehouseA),
            Movement(10m, 30m, warehouseId: _warehouseB)
        ]);

        lines.Should().HaveCount(2);
        lines.Single(x => x.WarehouseId == _warehouseA).AverageCost.Should().Be(10m);
        lines.Single(x => x.WarehouseId == _warehouseB).AverageCost.Should().Be(30m);
    }

    /// <summary>
    /// The inventory file is of the taxable entity, which has no warehouses in it — so the file's
    /// average is the total value over the total quantity, not the mean of the two averages.
    /// </summary>
    [Fact]
    public void ReplayByProduct_totals_the_warehouses_and_reweights_the_average()
    {
        var lines = StockValuation.ReplayByProduct(
        [
            Movement(90m, 10m, warehouseId: _warehouseA),
            Movement(10m, 100m, warehouseId: _warehouseB)
        ]);

        var line = lines.Should().ContainSingle().Subject;
        line.Quantity.Should().Be(100m);
        line.Value.Should().Be(1900m);
        // The mean of 10 and 100 would be 55, which is what a warehouse holding ten units would
        // have to weigh the same as one holding ninety.
        line.AverageCost.Should().Be(19m);
    }

    [Fact]
    public void ReplayByProduct_keeps_each_product_apart()
    {
        var lines = StockValuation.ReplayByProduct(
        [
            Movement(5m, 4m, productCode: "ART001"),
            Movement(5m, 8m, productCode: "ART002")
        ]);

        lines.Should().HaveCount(2);
        lines[0].ProductCode.Should().Be("ART001");
        lines[0].Value.Should().Be(20m);
        lines[1].Value.Should().Be(40m);
    }

    /// <summary>The description follows the most recent movement, so a renamed product reads right.</summary>
    [Fact]
    public void Replay_takes_the_description_from_the_last_movement()
    {
        var lines = StockValuation.Replay(
        [
            Movement(5m, 4m, description: "Nome antigo"),
            Movement(5m, 4m, description: "Nome novo")
        ]);

        lines.Single().ProductDescription.Should().Be("Nome novo");
    }

    /// <summary>
    /// A sale carries no cost of its own, so it leaves at the average. This is the case that makes
    /// the whole thing worth doing: nothing outside Purchasing ever knows what stock cost.
    /// </summary>
    [Fact]
    public void Replay_takes_a_sale_out_at_the_average()
    {
        var lines = StockValuation.Replay([Movement(10m, 10m), Movement(10m, 20m), Movement(-12m)]);

        var line = lines.Single();
        line.Quantity.Should().Be(8m);
        line.Value.Should().Be(120m);
        line.AverageCost.Should().Be(15m);
    }

    [Fact]
    public void Replay_of_nothing_is_nothing()
    {
        StockValuation.Replay([]).Should().BeEmpty();
        StockValuation.ReplayByProduct([]).Should().BeEmpty();
    }

    /// <summary>
    /// Entries written before costing existed carry no cost at all. They are treated like a sale —
    /// valued at whatever the average was — rather than being allowed to break the replay.
    /// </summary>
    [Fact]
    public void Replay_survives_movements_that_never_recorded_a_cost()
    {
        var lines = StockValuation.Replay([Movement(10m, unitCost: null), Movement(10m, 20m)]);

        var line = lines.Single();
        line.Quantity.Should().Be(20m);
        line.Value.Should().Be(200m);
        line.AverageCost.Should().Be(10m);
    }
}
