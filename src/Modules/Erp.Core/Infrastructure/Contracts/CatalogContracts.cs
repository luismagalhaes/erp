namespace Erp.Core.Infrastructure.Contracts;

// --- Classification: brands, families and subfamilies ---

public sealed record BrandDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record CreateBrandRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateBrandRequest(string Name, bool IsActive);

public sealed record ProductFamilyDto(Guid Id, string Code, string Name, bool IsActive, int SubfamilyCount);

public sealed record CreateProductFamilyRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateProductFamilyRequest(string Name, bool IsActive);

public sealed record ProductSubfamilyDto(
    Guid Id,
    Guid FamilyId,
    string FamilyName,
    string Code,
    string Name,
    bool IsActive);

public sealed record CreateProductSubfamilyRequest(Guid CompanyId, Guid FamilyId, string Code, string Name);

public sealed record UpdateProductSubfamilyRequest(Guid FamilyId, string Name, bool IsActive);

// --- Products ---

public sealed record ProductListItemDto(
    Guid Id,
    string ProductCode,
    string Description,
    string ProductType,
    string UnitOfMeasure,
    decimal UnitPrice,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    string? Barcode,
    Guid? FamilyId,
    string? FamilyName,
    Guid? SubfamilyId,
    string? SubfamilyName,
    Guid? BrandId,
    string? BrandName,
    bool IsActive,
    decimal UnitCost = 0m,
    string InventoryCategory = "M");

public sealed record CreateProductRequest(
    Guid CompanyId,
    string ProductCode,
    string Description,
    decimal UnitPrice,
    string ProductType = "P",
    string UnitOfMeasure = "UN",
    string DefaultTaxCountryRegion = "PT",
    string DefaultTaxCode = "NOR",
    decimal DefaultTaxPercentage = 23m,
    string? Barcode = null,
    Guid? FamilyId = null,
    Guid? SubfamilyId = null,
    Guid? BrandId = null,
    decimal UnitCost = 0m,
    string InventoryCategory = "M");

public sealed record UpdateProductRequest(
    string Description,
    decimal UnitPrice,
    string ProductType,
    string UnitOfMeasure,
    string DefaultTaxCountryRegion,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    string? Barcode,
    Guid? FamilyId,
    Guid? SubfamilyId,
    Guid? BrandId,
    bool IsActive,
    decimal UnitCost = 0m,
    string InventoryCategory = "M");

// --- Business partners ---

public sealed record PartnerDto(
    Guid Id,
    string Code,
    string Name,
    string TaxId,
    string? Address,
    string? PostalCode,
    string? City,
    string Country,
    string? Email,
    string? Phone,
    bool IsActive);

public sealed record CreatePartnerRequest(
    Guid CompanyId,
    string Code,
    string Name,
    string TaxId,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    string Country = "PT",
    string? Email = null,
    string? Phone = null);

public sealed record UpdatePartnerRequest(
    string Name,
    string TaxId,
    string? Address,
    string? PostalCode,
    string? City,
    string Country,
    string? Email,
    string? Phone,
    bool IsActive);
