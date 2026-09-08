namespace Erp.Sales.Domain;

/// <summary>
/// Taxable base and VAT totalled per fiscal space and rate. Feeds the I, J and K fields of
/// the QR code and the tax totals of the SAF-T file.
/// </summary>
public sealed class DocumentTaxSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public string TaxCountryRegion { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxableBase { get; set; }

    public decimal TaxAmount { get; set; }

    public SalesDocument Document { get; set; } = null!;
}
