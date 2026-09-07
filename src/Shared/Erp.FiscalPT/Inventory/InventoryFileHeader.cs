namespace Erp.FiscalPT.Inventory;

/// <summary>
/// The StockHeader of the inventory file: who is reporting, for what period, and whether there was
/// any stock at all.
/// </summary>
public sealed class InventoryFileHeader
{
    /// <summary>Tax id of the taxable entity, digits only. The schema takes it as an integer.</summary>
    public string TaxRegistrationNumber { get; init; } = string.Empty;

    /// <summary>The tax period the inventory refers to.</summary>
    public int FiscalYear { get; init; }

    /// <summary>Last day of the period: the stock reported is the stock held on this date.</summary>
    public DateOnly EndDate { get; init; }
}
