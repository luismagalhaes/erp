namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// A goods movement line with quantity still to invoice, so an invoice can be built from it.
/// </summary>
/// <param name="PendingQuantity">
/// What is left: the quantity moved, less what invoices already issued took from it.
/// </param>
public sealed record PendingMovementLineDto(
    Guid MovementId,
    Guid LineId,
    string DocumentNumber,
    string MovementType,
    DateOnly MovementDate,
    string PartyName,
    string PartyTaxId,
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal MovedQuantity,
    decimal InvoicedQuantity,
    decimal PendingQuantity,
    decimal UnitPrice,
    decimal DiscountPercentage,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    string? TaxExemptionCode,
    string? TaxExemptionReason);
