namespace Erp.Main.Models.Sales;

/// <param name="UnitPrice">Price before the line discount.</param>
/// <param name="LineAmount">Line total without VAT, after the discount.</param>
public sealed record InvoiceLine(
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
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    string? OriginatingNumber = null,
    DateOnly? OriginatingDate = null,
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);
