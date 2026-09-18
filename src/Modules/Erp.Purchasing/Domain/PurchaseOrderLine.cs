namespace Erp.Purchasing.Domain;

/// <summary>
/// One line of a purchase order. Product data is copied, not referenced: editing an article later
/// must not rewrite what was ordered at a price that was agreed.
/// </summary>
public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public int LineNumber { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    /// <summary>What the supplier is charging. Becomes the cost the goods enter stock with.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount agreed with the supplier, as a percentage of the gross line amount.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>The discount in currency, worked out from <see cref="DiscountPercentage"/>.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Line total without VAT, after the discount.</summary>
    public decimal LineAmount { get; set; }

    public string TaxCountryRegion { get; set; } = "PT";

    public string TaxCode { get; set; } = "NOR";

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>
    /// How much of this line has arrived. Kept on the line rather than derived, because a purchase
    /// order is not append-only — unlike the sales side, where the invoiced quantity is derived
    /// from the invoice lines precisely because the movement may never be touched again.
    /// </summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>What is still owed by the supplier. Never negative: over-delivery does not create a debt.</summary>
    public decimal PendingQuantity => Math.Max(0m, Quantity - ReceivedQuantity);

    public bool IsFullyReceived => ReceivedQuantity >= Quantity;

    public PurchaseOrder Order { get; set; } = null!;
}
