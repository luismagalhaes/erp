namespace Erp.Inventory.Infrastructure.Contracts;

public sealed record StockBalanceDto(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    DateTime LastMovementUtc);
