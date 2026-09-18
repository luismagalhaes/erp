namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>The VAT of a goods movement, grouped by rate — the same shape an invoice shows.</summary>
public sealed record MovementTaxDto(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);
