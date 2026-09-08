namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="Difference">
/// How much to add or take away. Positive brings stock in, negative takes it out; zero is refused,
/// because an adjustment that changes nothing is noise in the ledger.
/// </param>
/// <param name="UnitCost">
/// What the goods cost, when the adjustment knows. This is how a company that starts with a
/// warehouse already full gets its opening cost into the system — without it, stock that never came
/// in through a purchase would be valued at nothing forever. Left null, what comes in is worth the
/// average of what was already there.
/// </param>
public sealed record AdjustStockRequest(
    Guid CompanyId,
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Difference,
    DateOnly MovementDate,
    string Reason,
    decimal? UnitCost = null);
