namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="IsEcoFee">True when this line is an eco-fee ("Ecovalor") rather than an article.</param>
/// <param name="EcoFeeForLineNumber">
/// The 1-based position, in this same request, of the line this eco-fee was generated for. Required
/// when <paramref name="IsEcoFee"/> is true.
/// </param>
public sealed record CreateStockMovementLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxPercentage,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    decimal DiscountPercentage = 0m,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);
