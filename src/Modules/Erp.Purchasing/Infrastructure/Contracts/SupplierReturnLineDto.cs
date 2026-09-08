namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record SupplierReturnLineDto(
    Guid Id,
    int LineNumber,
    Guid ReceiptId,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitCost,
    decimal LineAmount);
