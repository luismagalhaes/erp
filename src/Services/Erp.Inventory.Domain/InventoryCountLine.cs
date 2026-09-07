namespace Erp.Inventory.Domain;

/// <summary>
/// One product in one warehouse, as counted. Keeps both what the system thought was there when the
/// count opened and what was actually found, because the pair is the point of the exercise.
/// </summary>
public sealed class InventoryCountLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CountId { get; set; }

    public Guid WarehouseId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    /// <summary>What the system held when the count opened.</summary>
    public decimal SystemQuantity { get; set; }

    public decimal CountedQuantity { get; set; }

    /// <summary>
    /// What was actually written to the ledger when the count closed, measured against the balance
    /// at that moment. Null while the count is open.
    /// </summary>
    /// <remarks>
    /// Not simply counted minus <see cref="SystemQuantity"/>: stock can move between opening and
    /// closing, and the adjustment has to land on the balance as it stands at closing, or it would
    /// silently undo those movements.
    /// </remarks>
    public decimal? AppliedDifference { get; set; }

    public InventoryCount Count { get; set; } = null!;
}
