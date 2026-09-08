namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record PurchaseInvoiceTaxSummaryDto(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);
