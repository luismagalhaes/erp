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

    /// <summary>Price before the line discount, as agreed with the customer.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount on the line, in percent of quantity times price.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>What the discount took off, exported as SAF-T SettlementAmount.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Line total without VAT, after the discount.</summary>
    public decimal LineAmount { get; set; }

    /// <summary>Quantity times price, before the discount: the "total ilíquido" of the line.</summary>
    public decimal GrossAmount => LineAmount + DiscountAmount;

    /// <summary>
    /// The unit price once the discount is taken off. SAF-T wants this one, so that quantity times
    /// price gives the amount the line is taxed on.
    /// </summary>
    public decimal NetUnitPrice =>
        Quantity == 0 ? UnitPrice : Math.Round(LineAmount / Quantity, 6, MidpointRounding.AwayFromZero);

    public string TaxCountryRegion { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public string? TaxExemptionCode { get; set; }

    public string? TaxExemptionReason { get; set; }

    // --- Origin, when the line comes from a goods movement ---

    /// <summary>
    /// The movement line this one invoices. How much of a movement is already invoiced is derived
    /// from these references, so nothing has to be updated on the movement itself — which is what
    /// keeps it append-only.
    /// </summary>
    public Guid? OriginatingLineId { get; set; }

    /// <summary>Number of the movement document, copied at issuing time for the SAF-T and the print.</summary>
    public string? OriginatingNumber { get; set; }

    public DateOnly? OriginatingDate { get; set; }

    // --- Eco-fee ("Ecovalor") lines ---

    /// <summary>
    /// True when this line is not an article but an eco-fee charged alongside one — a fee is never
    /// folded into the article's own line, so it always gets one of its own. Such a line carries no
    /// real stock, and <see cref="EcoFeeForLineId"/> says which line it belongs to.
    /// </summary>
    public bool IsEcoFee { get; set; }

    /// <summary>The line this eco-fee was generated for, when <see cref="IsEcoFee"/> is true.</summary>
    public Guid? EcoFeeForLineId { get; set; }

    public SalesDocument Document { get; set; } = null!;
}
