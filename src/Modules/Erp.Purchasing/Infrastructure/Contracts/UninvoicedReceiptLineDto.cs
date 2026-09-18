namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A receipt line that has not been invoiced yet. Recording an invoice starts from these, so the
/// goods are not brought into stock a second time.
/// </summary>
public sealed record UninvoicedReceiptLineDto(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    string? SupplierDocumentNumber,
    Guid SupplierId,
    string SupplierName,
    Guid ReceiptLineId,
    Guid? OrderLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal InvoicedQuantity,
    decimal PendingQuantity,
    decimal UnitCost,
    decimal DiscountPercentage);
