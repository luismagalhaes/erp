namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A receipt line with goods still in hand. A return starts from these — we can only send back what
/// we received and have not sent back already.
/// </summary>
public sealed record ReturnableReceiptLineDto(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal ReturnedQuantity,
    decimal ReturnableQuantity,
    decimal UnitCost);
