namespace Erp.FiscalPT.Inventory;

/// <summary>Everything that goes into one inventory communication file.</summary>
public sealed class InventoryFile
{
    /// <summary>
    /// Which schema to produce. Defaults to the valued one, which is what is required for periods
    /// from 2021 onwards.
    /// </summary>
    public InventoryFileVersion Version { get; init; } = InventoryFileVersion.Valued;

    public InventoryFileHeader Header { get; init; } = new();

    /// <summary>
    /// The stock held at the reference date. An empty list is legitimate: the communication is due
    /// even when the entity held nothing, and the header says so through NoStock.
    /// </summary>
    public IReadOnlyList<InventoryFileLine> Lines { get; init; } = [];
}
