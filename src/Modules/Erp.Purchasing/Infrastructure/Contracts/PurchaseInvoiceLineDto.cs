namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="MovesStock">
/// True when this line brought the goods in itself, because no receipt had already done it.
/// </param>
public sealed record PurchaseInvoiceLineDto(
    Guid Id,
    int LineNumber,
    Guid? ReceiptId,
    Guid? ReceiptLineId,
    Guid? OrderLineId,
    Guid? ReturnLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal LineAmount,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    string DeductionNature,
    bool MovesStock);
