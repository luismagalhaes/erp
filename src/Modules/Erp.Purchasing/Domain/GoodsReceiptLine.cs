namespace Erp.Purchasing.Domain;

/// <summary>
/// One line of a goods receipt: an article that arrived, in a quantity, at a cost.
/// </summary>
public sealed class GoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReceiptId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>
    /// The order line being received, when the goods were ordered. Null for a delivery that arrived
    /// without an order behind it, which happens and is not worth refusing.
    /// </summary>
    public Guid? OrderLineId { get; set; }

    public Guid? OrderId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    /// <summary>
    /// What the goods cost. Travels into the stock ledger, which is the only place a real cost
    /// enters the system — everything else values stock from the standard cost on the article.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Discount from the supplier's delivery note, as a percentage of the gross line amount. Kept
    /// here so an invoice integrating this receipt can carry it forward unchanged.
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>The discount in currency, worked out from <see cref="DiscountPercentage"/>.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Line total, after the discount. This is what enters the stock ledger.</summary>
    public decimal LineAmount { get; set; }

    public GoodsReceipt Receipt { get; set; } = null!;
}
