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

/// <summary>
/// The product as the listings see it. Written with init properties instead of a positional record
/// because EF Core cannot bind members over a constructor projection, which breaks any $orderby or
/// $filter the OData grids apply on top of the query. The constructor is kept for the in memory
/// mapping done after the writes.
/// </summary>
public sealed record ProductListItemDto
{
    public ProductListItemDto()
    {
    }

    public ProductListItemDto(
        Guid id,
        string productCode,
        string description,
        string productType,
        string unitOfMeasure,
        decimal unitPrice,
        string defaultTaxCode,
        decimal defaultTaxPercentage,
        string? barcode,
        Guid? familyId,
        string? familyName,
        Guid? subfamilyId,
        string? subfamilyName,
        Guid? brandId,
        string? brandName,
        bool isActive,
        decimal unitCost = 0m,
        string inventoryCategory = "M")
    {
        Id = id;
        ProductCode = productCode;
        Description = description;
        ProductType = productType;
        UnitOfMeasure = unitOfMeasure;
        UnitPrice = unitPrice;
        DefaultTaxCode = defaultTaxCode;
        DefaultTaxPercentage = defaultTaxPercentage;
        Barcode = barcode;
        FamilyId = familyId;
        FamilyName = familyName;
        SubfamilyId = subfamilyId;
        SubfamilyName = subfamilyName;
        BrandId = brandId;
        BrandName = brandName;
        IsActive = isActive;
        UnitCost = unitCost;
        InventoryCategory = inventoryCategory;
    }

    public Guid Id { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ProductType { get; init; } = string.Empty;

    public string UnitOfMeasure { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public string DefaultTaxCode { get; init; } = string.Empty;

    public decimal DefaultTaxPercentage { get; init; }

    public string? Barcode { get; init; }

    public Guid? FamilyId { get; init; }

    public string? FamilyName { get; init; }

    public Guid? SubfamilyId { get; init; }

    public string? SubfamilyName { get; init; }

    public Guid? BrandId { get; init; }

    public string? BrandName { get; init; }

    public bool IsActive { get; init; }

    public decimal UnitCost { get; init; }

    public string InventoryCategory { get; init; } = "M";
}

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
