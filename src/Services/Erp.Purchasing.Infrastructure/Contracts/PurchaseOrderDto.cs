namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record PurchaseOrderDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid WarehouseId,
    PurchaseOrderSupplierDto Supplier,
    string? Notes,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    string? ClosedReason,
    IReadOnlyList<PurchaseOrderLineDto> Lines);
