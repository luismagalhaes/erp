namespace Erp.Inventory.Domain;

/// <summary>Which way a stock movement goes.</summary>
public enum StockDirection : byte
{
    /// <summary>Goods coming in: purchases, returns from customers.</summary>
    In = 1,

    /// <summary>Goods going out: sales, deliveries, returns to suppliers.</summary>
    Out = 2,

    /// <summary>Correction from an inventory count. Can go either way, so the sign is on the entry.</summary>
    Adjustment = 3
}
