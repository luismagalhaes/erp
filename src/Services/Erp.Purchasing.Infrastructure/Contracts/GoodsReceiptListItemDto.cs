namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record GoodsReceiptListItemDto(
    Guid Id,
    string Number,
    string Status,
    DateOnly ReceiptDate,
    string SupplierName,
    string? SupplierDocumentNumber,
    int LineCount,
    decimal TotalCost,
    bool IsVoided);
