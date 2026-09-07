namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="Difference">
/// How much to add or take away. Positive brings stock in, negative takes it out; zero is refused,
/// because an adjustment that changes nothing is noise in the ledger.
/// </param>
public sealed record AdjustStockRequest(
    Guid CompanyId,
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Difference,
    DateOnly MovementDate,
    string Reason);
