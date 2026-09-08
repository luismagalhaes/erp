namespace Erp.Inventory.Domain;

/// <summary>
/// How much of a product a warehouse holds. A projection of <see cref="StockLedgerEntry"/>, kept
/// current in the same transaction as the entries, so reading stock does not mean summing the
/// whole ledger. The stock check recomputes it from the ledger and compares.
/// </summary>
public sealed class StockBalance
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductDescription { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    /// <summary>
    /// Weighted average cost of a unit. Derived from what came in and at what price, never written
    /// by hand — this is what replaces the standard cost on the product file as the source of
    /// valuation, because it is the only cost the system actually observed.
    /// </summary>
    public decimal AverageCost { get; private set; }

    /// <summary>What the quantity is worth. Always <c>Quantity × AverageCost</c>.</summary>
    public decimal StockValue { get; private set; }

    public DateTime LastMovementUtc { get; private set; }

    /// <summary>Guards against two movements of the same product overwriting each other.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Required by EF Core.</summary>
    private StockBalance()
    {
    }

    public static StockBalance Start(Guid companyId, Guid warehouseId, string productCode, string productDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        return new StockBalance
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ProductCode = productCode,
            ProductDescription = productDescription,
            Quantity = 0m,
            AverageCost = 0m,
            StockValue = 0m,
            LastMovementUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Applies a ledger entry, to the quantity and to the value together. The balance can go
    /// negative, and that is left visible rather than blocked: stock that went out without having
    /// come in is a real problem, and hiding it helps nobody.
    /// </summary>
    /// <remarks>
    /// The entry is <b>stamped</b> with the cost it moved at, which is why this takes the entry and
    /// not just its numbers. The rule itself is in <see cref="WeightedAverageCost"/>, shared with
    /// the replay that checks this projection.
    /// </remarks>
    public void Apply(StockLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var costing = new WeightedAverageCost(Quantity, StockValue, AverageCost);
        var unitCost = costing.CostOf(entry.UnitCost);

        entry.ValueAt(unitCost);

        var applied = costing.Apply(entry.Quantity, unitCost);

        Quantity = applied.Quantity;
        StockValue = applied.Value;
        AverageCost = applied.AverageCost;
        LastMovementUtc = entry.SystemEntryDateUtc;

        // The description follows the most recent movement, so a renamed product reads correctly.
        if (!string.IsNullOrWhiteSpace(entry.ProductDescription))
            ProductDescription = entry.ProductDescription;
    }
}
