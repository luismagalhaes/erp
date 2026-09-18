namespace Erp.Purchasing.Domain;

/// <summary>
/// One document settled by a payment. The number and date are copied here, so the payment still
/// reads correctly whatever happens to the document later.
/// </summary>
public sealed class SupplierPaymentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public int LineNumber { get; set; }

    public PayableDocumentKind DocumentKind { get; set; }

    /// <summary>The settled document. No foreign key: it may live in either of two tables.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>FT, FS or ND — the type on the document being paid.</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>The supplier's number, or ours for a self-billed invoice.</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    /// <summary>
    /// Amount of that document settled by this payment. Always positive, as on the paper; for a
    /// credit note it is how much of the credit this payment uses up.
    /// </summary>
    public decimal AppliedAmount { get; set; }

    /// <summary>
    /// True for a credit note. Its amount is taken off what the payment sends instead of added to
    /// it — derived from the type, so the sign cannot disagree with the document.
    /// </summary>
    public bool IsCredit => PurchaseDocumentTypes.IsCredit(DocumentType);

    /// <summary>What this line contributes to the money that leaves: negative for a credit note.</summary>
    public decimal SignedAmount => IsCredit ? -AppliedAmount : AppliedAmount;

    public SupplierPayment Payment { get; set; } = null!;
}
