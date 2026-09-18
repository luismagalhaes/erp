namespace Erp.Sales.Infrastructure.Contracts;

public sealed record InvoiceTaxDto(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);
