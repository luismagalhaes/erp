namespace Erp.Inventory.Domain;

/// <summary>One ledger movement, reduced to what valuing it needs.</summary>
/// <param name="UnitCost">
/// What the movement itself cost, when it knows: a purchase does, a sale does not. Null on the
/// entries written before costing existed, which the replay treats the same way as a sale.
/// </param>
public readonly record struct LedgerMovement(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal? UnitCost);

/// <summary>What one product is worth in one warehouse, at the end of the replayed movements.</summary>
public sealed record StockValuationLine(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal Value,
    decimal AverageCost,
    int MovementCount);

/// <summary>
/// Replays the ledger to find what stock is worth. The same arithmetic the running balance uses,
/// applied to the rows instead of kept alongside them.
/// </summary>
/// <remarks>
/// It exists because the two questions are different. The balance answers "what is it worth now";
/// this answers "what was it worth at the end of March", which the inventory file needs and no
/// stored figure can give. It is also what the stock check compares the stored figures against —
/// a projection is only trustworthy while something recomputes it.
/// </remarks>
public static class StockValuation
{
    /// <summary>
    /// <paramref name="movements"/> must already be in the order they happened: the average after a
    /// purchase depends on everything before it, so out-of-order rows give a different answer.
    /// </summary>
    public static IReadOnlyList<StockValuationLine> Replay(IEnumerable<LedgerMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);

        var running = new Dictionary<(Guid WarehouseId, string ProductCode), Running>();

        foreach (var movement in movements)
        {
            var key = (movement.WarehouseId, movement.ProductCode);

            if (!running.TryGetValue(key, out var current))
                current = new Running(WeightedAverageCost.Empty, movement.ProductDescription, 0);

            var costing = current.Costing;
            var applied = costing.Apply(movement.Quantity, costing.CostOf(movement.UnitCost));

            running[key] = current with
            {
                Costing = applied,
                // The description follows the most recent movement, so a renamed product reads
                // correctly — the same rule the stored balance obeys.
                Description = string.IsNullOrWhiteSpace(movement.ProductDescription)
                    ? current.Description
                    : movement.ProductDescription,
                Count = current.Count + 1
            };
        }

        return
        [
            .. running
                .Select(entry => new StockValuationLine(
                    entry.Key.WarehouseId,
                    entry.Key.ProductCode,
                    entry.Value.Description,
                    entry.Value.Costing.Quantity,
                    entry.Value.Costing.Value,
                    entry.Value.Costing.AverageCost,
                    entry.Value.Count))
                .OrderBy(line => line.ProductCode, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// The same replay, totalled per product across every warehouse. The inventory file is of the
    /// taxable entity, which has no warehouses in it.
    /// </summary>
    public static IReadOnlyList<StockValuationLine> ReplayByProduct(IEnumerable<LedgerMovement> movements) =>
        [
            .. Replay(movements)
                .GroupBy(line => line.ProductCode, StringComparer.Ordinal)
                .Select(group =>
                {
                    var quantity = group.Sum(line => line.Quantity);
                    var value = group.Sum(line => line.Value);

                    return new StockValuationLine(
                        Guid.Empty,
                        group.Key,
                        group.First().ProductDescription,
                        quantity,
                        value,
                        // Recomputed from the totals rather than averaged across warehouses: a
                        // mean of averages weights a warehouse holding one unit like one holding a
                        // thousand.
                        quantity == 0 ? 0m : Math.Round(value / quantity, 6, MidpointRounding.AwayFromZero),
                        group.Sum(line => line.MovementCount));
                })
                .OrderBy(line => line.ProductCode, StringComparer.Ordinal)
        ];

    private readonly record struct Running(WeightedAverageCost Costing, string Description, int Count);
}
