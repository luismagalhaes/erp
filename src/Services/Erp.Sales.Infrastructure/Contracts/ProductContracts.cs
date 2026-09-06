namespace Erp.Sales.Infrastructure.Contracts;

public sealed record ProductListItemDto(
    Guid Id,
    string ProductCode,
    string Description,
    string ProductType,
    string UnitOfMeasure,
    decimal UnitPrice,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    bool IsActive);

public sealed record CreateProductRequest(
    Guid CompanyId,
    string ProductCode,
    string Description,
    decimal UnitPrice,
    string ProductType = "P",
    string UnitOfMeasure = "UN",
    string DefaultTaxCountryRegion = "PT",
    string DefaultTaxCode = "NOR",
    decimal DefaultTaxPercentage = 23m);

public sealed record UpdateProductRequest(
    string Description,
    decimal UnitPrice,
    string ProductType,
    string UnitOfMeasure,
    string DefaultTaxCountryRegion,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    bool IsActive);
