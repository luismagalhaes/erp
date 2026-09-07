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
            LastMovementUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Applies a ledger entry. The quantity is signed, so this is a plain sum — the balance can
    /// go negative, and that is left visible rather than blocked: stock that went out without
    /// having come in is a real problem, and hiding it helps nobody.
    /// </summary>
    public void Apply(StockLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Quantity += entry.Quantity;
        LastMovementUtc = entry.SystemEntryDateUtc;

        // The description follows the most recent movement, so a renamed product reads correctly.
        if (!string.IsNullOrWhiteSpace(entry.ProductDescription))
            ProductDescription = entry.ProductDescription;
    }
}
