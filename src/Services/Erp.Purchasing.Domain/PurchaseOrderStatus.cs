namespace Erp.Purchasing.Domain;

public enum PurchaseOrderStatus : byte
{
    /// <summary>Being written. Nothing was sent to the supplier and everything can still change.</summary>
    Draft = 0,

    /// <summary>Sent to the supplier. Nothing received yet.</summary>
    Placed = 1,

    /// <summary>Some of it arrived, some is still owed.</summary>
    PartiallyReceived = 2,

    /// <summary>Everything ordered has arrived.</summary>
    Received = 3,

    /// <summary>
    /// Closed without being fully received: the rest is not coming. Closing is deliberate, so that
    /// what is still owed by a supplier is never a guess.
    /// </summary>
    Closed = 4,

    /// <summary>Called off before anything arrived.</summary>
    Cancelled = 5
}
