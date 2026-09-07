namespace Erp.Inventory.Domain;

/// <summary>
/// One movement of stock. The ledger is append-only, like the fiscal documents: a correction is a
/// new entry, never an edit of an old one. The balance in <see cref="StockBalance"/> is a
/// projection of these rows, which is what makes it possible to check one against the other.
/// </summary>
public sealed class StockLedgerEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid WarehouseId { get; private set; }

    /// <summary>Copied from the product file, like every other snapshot in this system.</summary>
    public string ProductCode { get; private set; } = string.Empty;

    public string ProductDescription { get; private set; } = string.Empty;

    public StockDirection Direction { get; private set; }

    /// <summary>
    /// Signed: positive brings stock in, negative takes it out. Keeping the sign here rather than
    /// deriving it from the direction means a balance is a plain sum, with nothing to get wrong.
    /// </summary>
    public decimal Quantity { get; private set; }

    public decimal? UnitCost { get; private set; }

    public DateOnly MovementDate { get; private set; }

    public DateTime SystemEntryDateUtc { get; private set; }

    // --- Origin ---

    public string? SourceDocumentType { get; private set; }

    public string? SourceDocumentNumber { get; private set; }

    public Guid? SourceDocumentId { get; private set; }

    /// <summary>
    /// The document line that moved this stock. Unique across the ledger: even if the rule that
    /// stops an integrating document from moving stock twice were to fail, the database would
    /// refuse the second entry.
    /// </summary>
    public Guid? SourceLineId { get; private set; }

    public Guid? InventoryCountId { get; private set; }

    public string? Reason { get; private set; }

    public string? CreatedByUserId { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private StockLedgerEntry()
    {
    }

    /// <summary>Records stock moved by a document.</summary>
    public static StockLedgerEntry FromDocument(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        string productDescription,
        StockDirection direction,
        decimal quantity,
        DateOnly movementDate,
        string documentType,
        string documentNumber,
        Guid documentId,
        Guid documentLineId,
        decimal? unitCost = null,
        string? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (direction == StockDirection.Adjustment)
            throw new ArgumentException("A document movement is an in or an out, never an adjustment.", nameof(direction));

        return new StockLedgerEntry
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ProductCode = productCode,
            ProductDescription = productDescription,
            Direction = direction,
            Quantity = direction == StockDirection.In ? quantity : -quantity,
            UnitCost = unitCost,
            MovementDate = movementDate,
            SystemEntryDateUtc = DateTime.UtcNow,
            SourceDocumentType = documentType,
            SourceDocumentNumber = documentNumber,
            SourceDocumentId = documentId,
            SourceLineId = documentLineId,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// Undoes a movement by recording the opposite one. The ledger is append-only, so voiding a
    /// document never removes what it wrote: it adds the entry that cancels it out, and both stay
    /// visible.
    /// </summary>
    /// <remarks>
    /// The reversal carries no <see cref="SourceLineId"/>. That column is unique, and it means
    /// "this line has moved its stock" — which stays true of the original line even after the
    /// document is voided.
    /// </remarks>
    public static StockLedgerEntry Reverse(StockLedgerEntry original, string reason, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new StockLedgerEntry
        {
            CompanyId = original.CompanyId,
            WarehouseId = original.WarehouseId,
            ProductCode = original.ProductCode,
            ProductDescription = original.ProductDescription,
            Direction = original.Direction switch
            {
                StockDirection.In => StockDirection.Out,
                StockDirection.Out => StockDirection.In,
                _ => StockDirection.Adjustment
            },
            Quantity = -original.Quantity,
            UnitCost = original.UnitCost,
            MovementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SystemEntryDateUtc = DateTime.UtcNow,
            SourceDocumentType = original.SourceDocumentType,
            SourceDocumentNumber = original.SourceDocumentNumber,
            SourceDocumentId = original.SourceDocumentId,
            InventoryCountId = original.InventoryCountId,
            Reason = reason,
            CreatedByUserId = userId
        };
    }

    /// <summary>
    /// Records a correction, from an inventory count or from a manual adjustment. The quantity is
    /// the difference, so it carries its own sign.
    /// </summary>
    public static StockLedgerEntry FromAdjustment(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        string productDescription,
        decimal difference,
        DateOnly movementDate,
        string reason,
        Guid? inventoryCountId = null,
        string? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (difference == 0)
            throw new ArgumentException("An adjustment of zero changes nothing and is not recorded.", nameof(difference));

        return new StockLedgerEntry
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ProductCode = productCode,
            ProductDescription = productDescription,
            Direction = StockDirection.Adjustment,
            Quantity = difference,
            MovementDate = movementDate,
            SystemEntryDateUtc = DateTime.UtcNow,
            InventoryCountId = inventoryCountId,
            Reason = reason,
            CreatedByUserId = createdByUserId
        };
    }
}
