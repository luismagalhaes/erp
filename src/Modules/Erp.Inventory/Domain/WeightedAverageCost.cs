namespace Erp.Inventory.Domain;

/// <summary>
/// The weighted average cost of one product in one warehouse: how much is held, what it is worth,
/// and therefore what a unit of it costs.
/// </summary>
/// <remarks>
/// A pure value with no idea of where the movements come from, so the same arithmetic serves the
/// running balance and the replay of the ledger — and the two can be compared against each other.
/// <para>
/// The rule is one sentence: <b>a movement that carries its own cost moves value at that cost, and
/// one that does not moves value at the average</b>. A purchase carries a cost, a sale does not, and
/// that is the whole of it. Reversing a purchase carries the original cost back out, which is what
/// stops the classic drift of undoing a cheap purchase at today's dearer average.
/// </para>
/// </remarks>
/// <param name="Quantity">Signed: it can be negative, and that is left visible.</param>
/// <param name="Value">What the quantity is worth. Always <c>Quantity × AverageCost</c>.</param>
public readonly record struct WeightedAverageCost(decimal Quantity, decimal Value, decimal AverageCost)
{
    /// <summary>
    /// Costs are kept to six decimals — the precision of the columns — because rounding an average
    /// to cents and then multiplying it back out loses money one movement at a time.
    /// </summary>
    private const int Decimals = 6;

    public static readonly WeightedAverageCost Empty = new(0m, 0m, 0m);

    /// <summary>
    /// What a movement is worth per unit: its own cost when it has one, and the current average
    /// when it has not.
    /// </summary>
    public decimal CostOf(decimal? movementUnitCost) => movementUnitCost ?? AverageCost;

    /// <summary>
    /// Applies a movement. <paramref name="signedQuantity"/> is positive coming in and negative
    /// going out, as the ledger stores it.
    /// </summary>
    public WeightedAverageCost Apply(decimal signedQuantity, decimal unitCost)
    {
        var quantity = Quantity + signedQuantity;
        var value = Round(Value + signedQuantity * unitCost);

        // Nothing left. Any value still showing is rounding, not stock, so it goes; the average
        // stays as the last thing known, which is what the next movement out would leave at.
        if (quantity == 0)
            return new WeightedAverageCost(0m, 0m, AverageCost);

        // Stock that went out without having come in. The average of nothing means nothing, so it
        // is frozen and the value follows it — a negative quantity beside a positive value would
        // read as a second, separate fault.
        if (quantity < 0)
            return new WeightedAverageCost(quantity, Round(quantity * AverageCost), AverageCost);

        return new WeightedAverageCost(quantity, value, Round(value / quantity));
    }

    private static decimal Round(decimal value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
