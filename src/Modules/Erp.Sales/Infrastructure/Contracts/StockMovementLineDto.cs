namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="UnitPrice">Price before the line discount.</param>
/// <param name="LineAmount">Line total without VAT, after the discount.</param>
public sealed record StockMovementLineDto(
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);
