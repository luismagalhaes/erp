namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record GoodsReceiptLineDto(
    Guid Id,
    int LineNumber,
    Guid? OrderId,
    Guid? OrderLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitCost,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal LineAmount);
