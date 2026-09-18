namespace Erp.Main.Models.Sales;

public sealed record InvoiceTax(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);
