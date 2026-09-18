namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="UnitPrice">Price before the line discount.</param>
/// <param name="TaxExemptionCode">
/// Required when <paramref name="TaxCode"/> is ISE: a code from the tax authority's table (M01 to M99).
/// </param>
/// <param name="TaxExemptionReason">Defaults to the legal basis the table gives for the code.</param>
/// <param name="OriginatingLineId">
/// The goods movement line this one invoices, when the invoice comes from a delivery note.
/// </param>
/// <param name="DiscountPercentage">Line discount, from 0 to 100.</param>
/// <param name="IsEcoFee">True when this line is an eco-fee ("Ecovalor") rather than an article.</param>
/// <param name="EcoFeeForLineNumber">
/// The 1-based position, in this same request, of the line this eco-fee was generated for. Required
/// when <paramref name="IsEcoFee"/> is true.
/// </param>
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
