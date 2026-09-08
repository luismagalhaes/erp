namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record GoodsReceiptDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly ReceiptDate,
    Guid WarehouseId,
    string? SupplierDocumentNumber,
    DateOnly? SupplierDocumentDate,
    PurchaseOrderSupplierDto Supplier,
    string? Notes,
    decimal TotalCost,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<GoodsReceiptLineDto> Lines);
