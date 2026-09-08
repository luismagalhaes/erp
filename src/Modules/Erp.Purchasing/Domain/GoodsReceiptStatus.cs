namespace Erp.Purchasing.Domain;

public enum GoodsReceiptStatus : byte
{
    /// <summary>The goods came in and the stock moved.</summary>
    Received = 0,

    /// <summary>
    /// Undone. The stock it brought in was taken back out and the order was credited with the
    /// quantity again. The receipt itself stays, with its reason.
    /// </summary>
    Voided = 1
}
