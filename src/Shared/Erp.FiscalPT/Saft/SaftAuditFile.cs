namespace Erp.FiscalPT.Saft;

/// <summary>
/// Everything that goes into one SAF-T (PT) file. The caller fills it from its own data; the
/// nesting into MasterFiles and SourceDocuments, and the control totals, are the writer's job.
/// </summary>
public sealed class SaftAuditFile
{
    public SaftHeader Header { get; init; } = new();

    public IReadOnlyList<SaftCustomer> Customers { get; init; } = [];

    public IReadOnlyList<SaftProduct> Products { get; init; } = [];

    public IReadOnlyList<SaftTaxTableEntry> TaxTable { get; init; } = [];

    public IReadOnlyList<SaftInvoice> Invoices { get; init; } = [];

    public IReadOnlyList<SaftStockMovement> StockMovements { get; init; } = [];

    public IReadOnlyList<SaftPayment> Payments { get; init; } = [];
}
