namespace Erp.Inventory.Infrastructure.Contracts;

public sealed record InventoryCountDto(
    Guid Id,
    Guid CompanyId,
    Guid? WarehouseId,
    string Reference,
    string Scope,
    string Status,
    DateOnly CountDate,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    int LineCount,
    int LinesWithDifference,
    IReadOnlyList<InventoryCountLineDto> Lines);
