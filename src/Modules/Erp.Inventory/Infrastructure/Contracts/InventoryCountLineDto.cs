namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="AppliedDifference">
/// What was written to the ledger when the count closed. Null while it is open.
/// </param>
public sealed record InventoryCountLineDto(
    Guid Id,
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal SystemQuantity,
    decimal CountedQuantity,
    decimal? AppliedDifference);
