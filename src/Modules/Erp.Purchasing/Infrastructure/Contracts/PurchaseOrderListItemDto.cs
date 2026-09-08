namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>A row of the order list. Deliberately without lines, which the list never shows.</summary>
public sealed record PurchaseOrderListItemDto(
    Guid Id,
    string Number,
    string Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    string SupplierName,
    string SupplierTaxId,
    int LineCount,
    decimal GrossTotal,
    bool IsOpen);
