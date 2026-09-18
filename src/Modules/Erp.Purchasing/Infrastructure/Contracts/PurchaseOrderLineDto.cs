namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="PendingQuantity">What the supplier still owes on this line.</param>
public sealed record PurchaseOrderLineDto(
    Guid Id,
    int LineNumber,
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
    decimal ReceivedQuantity,
    decimal PendingQuantity);
