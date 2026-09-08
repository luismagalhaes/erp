namespace Erp.SeriesRegistry.Domain;

/// <summary>
/// What documents of a series do to stock. A property of the series, not of the document type: the
/// same type is used differently by different businesses.
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
