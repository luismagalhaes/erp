namespace Erp.Main.Models.Sales;

public sealed record CreateInvoiceLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxPercentage,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    Guid? OriginatingLineId = null,
    decimal DiscountPercentage = 0m,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);
