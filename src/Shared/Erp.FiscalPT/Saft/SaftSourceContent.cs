namespace Erp.FiscalPT.Saft;

/// <summary>What one module contributes to a SAF-T file.</summary>
/// <remarks>
/// The master files travel with the documents rather than being derived from them: a
/// <see cref="SaftInvoice"/> carries only the customer's tax id, and the name and address that the
/// Customer table needs live on the module's own documents. Each source therefore builds its own,
/// and whoever assembles the file removes the duplicates.
/// </remarks>
public sealed record SaftSourceContent(
    IReadOnlyList<SaftInvoice> Invoices,
    IReadOnlyList<SaftStockMovement> StockMovements,
    IReadOnlyList<SaftPayment> Payments,
    IReadOnlyList<SaftCustomer> Customers,
    IReadOnlyList<SaftProduct> Products,
    IReadOnlyList<SaftTaxTableEntry> TaxTable)
{
    public static SaftSourceContent Empty { get; } = new([], [], [], [], [], []);

    public int DocumentCount => Invoices.Count + StockMovements.Count + Payments.Count;
}
