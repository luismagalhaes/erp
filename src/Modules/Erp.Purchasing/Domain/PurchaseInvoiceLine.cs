namespace Erp.Purchasing.Domain;

/// <summary>
/// One line of a supplier's invoice, as it appears on their document.
/// </summary>
public sealed class PurchaseInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>
    /// The receipt line this invoices. When it is set, the goods already came into stock on the
    /// receipt and this line must not bring them in a second time.
    /// </summary>
    public Guid? ReceiptLineId { get; set; }

    public Guid? ReceiptId { get; set; }

    public Guid? OrderLineId { get; set; }

    /// <summary>
    /// The return line this credits, on a supplier's credit note. Traceability only: the stock
    /// already left on the return, and the credit note is about the money.
    /// </summary>
    public Guid? ReturnLineId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Discount as it appears on the supplier's document. When this line comes from a receipt or
    /// order, it is carried forward unchanged \u2014 the integrating document must not silently drop it.
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>The discount in currency, worked out from <see cref="DiscountPercentage"/>.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Line total without VAT, after the discount.</summary>
    public decimal LineAmount { get; set; }

    public string TaxCountryRegion { get; set; } = "PT";

    public string TaxCode { get; set; } = "NOR";

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>What the purchase was for. Only <see cref="DeductionNature.Inventory"/> moves stock.</summary>
    public DeductionNature DeductionNature { get; set; } = DeductionNature.Inventory;

    /// <summary>True when this line has to bring the goods into stock itself.</summary>
    public bool MovesStock => DeductionNature == DeductionNature.Inventory && ReceiptLineId is null;

    public PurchaseInvoice Invoice { get; set; } = null!;
}
