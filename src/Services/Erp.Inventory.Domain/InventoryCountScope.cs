namespace Erp.Inventory.Domain;

/// <summary>How much of the stock a count covers.</summary>
public enum InventoryCountScope : byte
{
    /// <summary>Everything the company holds, in every warehouse.</summary>
    Total = 1,

    /// <summary>Everything in one warehouse.</summary>
    Warehouse = 2,

    /// <summary>A chosen list of products.</summary>
    Products = 3
}
