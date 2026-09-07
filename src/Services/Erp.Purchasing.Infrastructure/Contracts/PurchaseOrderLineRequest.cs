namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record PurchaseOrderLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string TaxCode = "NOR",
    decimal TaxPercentage = 23m);
