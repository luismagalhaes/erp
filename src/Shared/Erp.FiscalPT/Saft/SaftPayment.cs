namespace Erp.FiscalPT.Saft;

/// <summary>A receipt, exported under SourceDocuments/Payments.</summary>
public sealed class SaftPayment
{
    public string PaymentRefNo { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public int Period { get; init; }

    public DateOnly TransactionDate { get; init; }

    /// <summary>RC or RG.</summary>
    public string PaymentType { get; init; } = string.Empty;

    public string? Description { get; init; }

    public SaftDocumentStatus DocumentStatus { get; init; } = new();

    public IReadOnlyList<SaftPaymentMethod> PaymentMethods { get; init; } = [];

    public string SourceId { get; init; } = SaftConstants.Unknown;

    public DateTime SystemEntryDate { get; init; }

    public string CustomerId { get; init; } = string.Empty;

    public IReadOnlyList<SaftPaymentLine> Lines { get; init; } = [];

    public SaftDocumentTotals Totals { get; init; } = new();
}
