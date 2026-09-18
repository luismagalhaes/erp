namespace Erp.Sales.Domain;

/// <summary>
/// Line of a goods movement. Product data is copied here at issuing time, never referenced by
/// key, so changing an item later cannot rewrite documents already issued.
/// </summary>
public sealed class StockMovementLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MovementId { get; set; }

    public int LineNumber { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    /// <summary>Price before the line discount.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount on the line, in percent of quantity times price.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>What the discount took off, exported as SAF-T SettlementAmount.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Quantity times price, before the discount.</summary>
    public decimal GrossAmount => LineAmount + DiscountAmount;

    /// <summary>The unit price once the discount is taken off, which is what SAF-T wants.</summary>
    public decimal NetUnitPrice =>
        Quantity == 0 ? UnitPrice : Math.Round(LineAmount / Quantity, 6, MidpointRounding.AwayFromZero);

    /// <summary>Line total without VAT.</summary>
    public decimal LineAmount { get; set; }

    public string TaxCountryRegion { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public string? TaxExemptionCode { get; set; }

    public string? TaxExemptionReason { get; set; }

    // --- Eco-fee ("Ecovalor") lines ---

    /// <summary>
    /// True when this line is not an article but an eco-fee charged alongside one — a fee is never
    /// folded into the article's own line, so it always gets one of its own. Such a line carries no
    /// real stock, and <see cref="EcoFeeForLineId"/> says which line it belongs to.
    /// </summary>
    public bool IsEcoFee { get; set; }

    /// <summary>The line this eco-fee was generated for, when <see cref="IsEcoFee"/> is true.</summary>
    public Guid? EcoFeeForLineId { get; set; }

    public StockMovement Movement { get; set; } = null!;
}
