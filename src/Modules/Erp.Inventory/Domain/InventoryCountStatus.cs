namespace Erp.Inventory.Domain;

/// <summary>Where a count is in its life.</summary>
public enum InventoryCountStatus : byte
{
    /// <summary>Being counted. Quantities can still be changed and nothing has moved yet.</summary>
    Open = 1,

    /// <summary>Closed: the differences have been written to the ledger and are now history.</summary>
    Closed = 2
}
