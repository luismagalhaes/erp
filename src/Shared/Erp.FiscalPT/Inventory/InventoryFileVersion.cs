namespace Erp.FiscalPT.Inventory;

/// <summary>
/// Which version of the inventory file to produce. The communication can be made valued or not,
/// and the two are different schemas rather than a flag inside one.
/// </summary>
public enum InventoryFileVersion : byte
{
    /// <summary>
    /// Schema 1_02: quantities only, no valuation. The format for periods before 2021.
    /// </summary>
    QuantitiesOnly = 1,

    /// <summary>
    /// Schema 2_01: carries ClosingStockValue as well, and adds the biological assets category.
    /// Mandatory for tax periods from 2021 onwards.
    /// </summary>
    Valued = 2
}
