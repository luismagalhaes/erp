namespace Erp.Purchasing.Domain;

/// <summary>One article going back to the supplier.</summary>
public sealed class SupplierReturnLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReturnId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>
    /// The receipt line the goods came in on. Always set: we can only send back what we received,
    /// and this is what says how much of it is left to send.
    /// </summary>
    public Guid ReceiptLineId { get; set; }

    public Guid ReceiptId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "UN";

    /// <summary>What the goods came in at. The stock leaves at the cost it entered.</summary>
    public decimal UnitCost { get; set; }

    public decimal LineAmount { get; set; }

    public SupplierReturn Return { get; set; } = null!;
}
