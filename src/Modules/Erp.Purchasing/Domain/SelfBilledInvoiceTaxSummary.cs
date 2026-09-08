namespace Erp.Purchasing.Domain;

/// <summary>
/// Tax totalled by rate, as the SAF-T and the QR code need it. Stored rather than recomputed,
/// because it is part of what was signed.
/// </summary>
public sealed class SelfBilledInvoiceTaxSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public string TaxCountryRegion { get; set; } = "PT";

    public string TaxCode { get; set; } = "NOR";

    public decimal TaxPercentage { get; set; }

    public decimal TaxableBase { get; set; }

    public decimal TaxAmount { get; set; }

    public SelfBilledInvoice Invoice { get; set; } = null!;
}
