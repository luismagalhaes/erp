namespace Erp.Sales.Domain;

/// <summary>
/// Document line. Product data is copied here at issuing time, never referenced by key,
/// so changing an item later cannot rewrite documents already issued.
/// </summary>
public sealed class SalesDocumentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public int LineNumber { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    public decimal UnitPrice { get; set; }

    /// <summary>Line total without VAT.</summary>
    public decimal LineAmount { get; set; }

    public string TaxCountryRegion { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public string? TaxExemptionCode { get; set; }

    public string? TaxExemptionReason { get; set; }

    public SalesDocument Document { get; set; } = null!;
}
