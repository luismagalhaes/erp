namespace Erp.Purchasing.Domain;

/// <summary>
/// The invoice broken down by rate. Derived from the lines, but stored: the periodic VAT return is
/// filled from this, and it has to keep saying what the supplier's document said even if the tax
/// table changes afterwards.
/// </summary>
public sealed class PurchaseInvoiceTaxSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public string TaxCountryRegion { get; set; } = "PT";

    public string TaxCode { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxableBase { get; set; }

    public decimal TaxAmount { get; set; }

    public PurchaseInvoice Invoice { get; set; } = null!;
}
