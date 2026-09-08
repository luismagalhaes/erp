namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="AverageCost">
/// Weighted average cost of a unit, derived from what came in and at what price. Zero means nothing
/// costed has ever come in — not that the goods are free.
/// </param>
public sealed record StockBalanceDto(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal AverageCost,
    decimal StockValue,
    DateTime LastMovementUtc);
