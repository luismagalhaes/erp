namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record SupplierReturnDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly ReturnDate,
    Guid WarehouseId,
    string Reason,
    PurchaseOrderSupplierDto Supplier,
    string? Notes,
    decimal TotalCost,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<SupplierReturnLineDto> Lines);
