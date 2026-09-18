namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="OrderLineId">
/// The order line being received. Null for goods that arrived without an order behind them, which
/// happens and is not worth refusing.
/// </param>
public sealed record GoodsReceiptLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitCost,
    string UnitOfMeasure = "UN",
    Guid? OrderLineId = null,
    decimal DiscountPercentage = 0m);
