namespace Erp.Purchasing.Domain;

/// <summary>
/// One line of an invoice we issued on the supplier's behalf.
/// </summary>
/// <remarks>
/// It does nothing to stock. The goods came in on the goods receipt, and the self-billed invoice is
/// about the money — the same split the supplier's own invoice already obeys.
/// </remarks>
public sealed class SelfBilledInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>The goods receipt line being paid for, when the invoice was raised from one.</summary>
    public Guid? ReceiptLineId { get; set; }

    public Guid? ReceiptId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    public decimal UnitPrice { get; set; }

    /// <summary>Line total without VAT.</summary>
    public decimal LineAmount { get; set; }

    public string TaxCountryRegion { get; set; } = "PT";

    public string TaxCode { get; set; } = "NOR";

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public string? TaxExemptionCode { get; set; }

    public string? TaxExemptionReason { get; set; }

    public SelfBilledInvoice Invoice { get; set; } = null!;
}
