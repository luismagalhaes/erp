namespace Erp.Inventory.Domain;

/// <summary>
/// A stock count, total or partial. Opening it takes a picture of what the system holds; closing it
/// writes the differences to the ledger as adjustments.
/// </summary>
/// <remarks>
/// Zeroing the stock is not a separate mechanism: it is a count opened with every counted quantity
/// at zero. One path, auditable the same way, instead of two that can drift apart.
/// </remarks>
public sealed class InventoryCount
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    /// <summary>The warehouse being counted, or null when the count spans all of them.</summary>
    public Guid? WarehouseId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public InventoryCountScope Scope { get; private set; }

    public InventoryCountStatus Status { get; private set; } = InventoryCountStatus.Open;

    public DateOnly CountDate { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? ClosedByUserId { get; private set; }

    public ICollection<InventoryCountLine> Lines { get; private set; } = [];

    public bool IsOpen => Status == InventoryCountStatus.Open;

    /// <summary>Required by EF Core.</summary>
    private InventoryCount()
    {
    }

    /// <summary>
    /// Opens a count over the given stock. Each line starts at what the system holds, or at zero
    /// when the count is meant to clear the warehouse before a fresh count.
    /// </summary>
    /// <param name="startAtZero">
    /// True to zero every counted quantity. Closing such a count empties the stock in scope, which
    /// is the "zerar" step before recounting from nothing.
    /// </param>
    public static InventoryCount Open(
        Guid companyId,
        Guid? warehouseId,
        InventoryCountScope scope,
        string reference,
        DateOnly countDate,
        IReadOnlyList<InventoryCountLine> lines,
        bool startAtZero,
        string? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(lines);

        if (scope == InventoryCountScope.Warehouse && warehouseId is null)
            throw new ArgumentException("A count of one warehouse has to say which.", nameof(warehouseId));

        var count = new InventoryCount
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            Scope = scope,
            Reference = reference.Trim(),
            CountDate = countDate,
            CreatedByUserId = createdByUserId
        };

        foreach (var line in lines)
        {
            line.CountId = count.Id;
            line.CountedQuantity = startAtZero ? 0m : line.SystemQuantity;
            count.Lines.Add(line);
        }

        return count;
    }

    /// <summary>Records what was found. Only possible while the count is open.</summary>
    public void SetCounted(Guid lineId, decimal countedQuantity)
    {
        EnsureOpen();

        var line = Lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new ArgumentException($"Line '{lineId}' does not belong to this count.", nameof(lineId));

        line.CountedQuantity = countedQuantity;
    }

    /// <summary>
    /// Closes the count. The caller supplies the balance each line stands at now, and gets back the
    /// difference to write to the ledger for each line that actually moves.
    /// </summary>
    /// <param name="currentQuantities">
    /// Balance per line id, read at closing time. Measuring against this rather than against the
    /// opening picture is what stops the count from undoing movements made while it was open.
    /// </param>
    public IReadOnlyList<(InventoryCountLine Line, decimal Difference)> Close(
        IReadOnlyDictionary<Guid, decimal> currentQuantities,
        string? closedByUserId,
        DateTime closedAtUtc)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(currentQuantities);

        var adjustments = new List<(InventoryCountLine, decimal)>();

        foreach (var line in Lines)
        {
            var current = currentQuantities.TryGetValue(line.Id, out var quantity) ? quantity : 0m;
            var difference = line.CountedQuantity - current;

            line.AppliedDifference = difference;

            if (difference != 0)
                adjustments.Add((line, difference));
        }

        Status = InventoryCountStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        ClosedByUserId = closedByUserId;

        return adjustments;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException($"Count '{Reference}' is closed and cannot be changed.");
    }
}
