namespace Erp.Sales.Domain;

/// <summary>
/// What a document issued from a series does to stock. Configured on the series, because that is
/// where the behaviour of a document type is already decided.
/// </summary>
public enum StockEffect : byte
{
    /// <summary>Moves no stock. Debit notes and own-asset movements, for instance.</summary>
    None = 0,

    /// <summary>Brings goods in: returns from customers, credit notes for returned goods.</summary>
    In = 1,

    /// <summary>Takes goods out: deliveries, transport notes, direct sales.</summary>
    Out = 2
}
