namespace Erp.Sales.Domain;

/// <summary>
/// One invoice settled by this receipt, exported as a SAF-T Payment line with its
/// SourceDocumentID. The invoice number and date are copied here, so the receipt still reads
/// correctly whatever happens later.
/// </summary>
public sealed class PaymentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>The settled document. Kept for navigation; the receipt does not depend on it.</summary>
    public Guid OriginatingDocumentId { get; set; }

    /// <summary>SAF-T OriginatingON, e.g. "FT A2026/2".</summary>
    public string OriginatingNumber { get; set; } = string.Empty;

    public DateOnly OriginatingDate { get; set; }

    /// <summary>Amount of that invoice settled by this receipt.</summary>
    public decimal AppliedAmount { get; set; }

    public Payment Payment { get; set; } = null!;
}
