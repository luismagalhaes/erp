namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// A product to add to an open count sheet, because the system did not know it was there.
/// </summary>
/// <param name="UnitCost">
/// What the goods cost. The ledger has never seen these, so nothing else can say what they are
/// worth — this is the door through which opening stock gets its cost. Left null they enter at
/// whatever average the product already carries, which for something genuinely new is nothing.
/// </param>
public sealed record AddCountLineRequest(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal? UnitCost = null);
