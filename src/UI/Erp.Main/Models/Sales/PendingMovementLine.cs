namespace Erp.Main.Models.Sales;

/// <summary>A delivery note line with quantity still to invoice.</summary>
public sealed record PendingMovementLine(
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
