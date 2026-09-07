namespace Erp.FiscalPT.Saft;

/// <summary>An invoicing document, exported under SourceDocuments/SalesInvoices.</summary>
public sealed class SaftInvoice
{
    public string InvoiceNo { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public SaftDocumentStatus DocumentStatus { get; init; } = new();

    public string Hash { get; init; } = string.Empty;

    public string HashControl { get; init; } = string.Empty;

    /// <summary>Month of the document date, 1 to 12.</summary>
    public int Period { get; init; }

    public DateOnly InvoiceDate { get; init; }

    /// <summary>FT, FS, FR, NC or ND.</summary>
    public string InvoiceType { get; init; } = string.Empty;

    public bool SelfBilling { get; init; }

    public bool CashVatScheme { get; init; }

    public bool ThirdPartiesBilling { get; init; }

    public string SourceId { get; init; } = SaftConstants.Unknown;

    public DateTime SystemEntryDate { get; init; }

    public string CustomerId { get; init; } = string.Empty;

    public IReadOnlyList<SaftInvoiceLine> Lines { get; init; } = [];

    public SaftDocumentTotals Totals { get; init; } = new();
}
