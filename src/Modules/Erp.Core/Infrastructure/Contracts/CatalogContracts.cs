namespace Erp.Core.Infrastructure.Contracts;

// --- Classification: brands, families and subfamilies ---

/// <summary>
/// The brand as the listings see it. Written with init properties instead of a positional record
/// because EF Core cannot bind members over a constructor projection, which breaks any $orderby or
/// $filter the OData grids apply on top of the query. The constructor is kept for the in memory
/// mapping done after the writes.
/// </summary>
public sealed record BrandDto
{
    public BrandDto()
    {
    }

    public BrandDto(Guid id, string code, string name, bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed record CreateBrandRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateBrandRequest(string Name, bool IsActive);

/// <summary>
/// The family as the listings see it. Init properties for the same reason as <see cref="BrandDto"/>:
/// EF Core only maps members over an object initializer, which is what keeps the OData options
/// translatable to SQL.
/// </summary>
public sealed record ProductFamilyDto
{
    public ProductFamilyDto()
    {
    }

    public ProductFamilyDto(Guid id, string code, string name, bool isActive, int subfamilyCount)
    {
        Id = id;
        Code = code;
        Name = name;
        IsActive = isActive;
        SubfamilyCount = subfamilyCount;
    }

    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int SubfamilyCount { get; init; }
}

public sealed record CreateProductFamilyRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateProductFamilyRequest(string Name, bool IsActive);

/// <summary>
/// The subfamily as the listings see it, carrying the family name so the grid does not need a
/// second round trip. Init properties keep the projection translatable by EF Core and OData.
/// </summary>
public sealed record ProductSubfamilyDto
{
    public ProductSubfamilyDto()
    {
    }

    public ProductSubfamilyDto(
        Guid id,
        Guid familyId,
        string familyName,
        string code,
        string name,
        bool isActive)
    {
        Id = id;
        FamilyId = familyId;
        FamilyName = familyName;
        Code = code;
        Name = name;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public Guid FamilyId { get; init; }
    public string FamilyName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

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

/// <summary>
/// Customers and suppliers share the same shape, so they share the same listing DTO. Init
/// properties for the same reason as <see cref="BrandDto"/>: EF Core only maps members over an
/// object initializer, which is what keeps the OData options translatable to SQL.
/// </summary>
public sealed record PartnerDto
{
    public PartnerDto()
    {
    }

    public PartnerDto(
        Guid id,
        string code,
        string name,
        string taxId,
        string? address,
        string? postalCode,
        string? city,
        string country,
        string? email,
        string? phone,
        bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        TaxId = taxId;
        Address = address;
        PostalCode = postalCode;
        City = city;
        Country = country;
        Email = email;
        Phone = phone;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public string? City { get; init; }
    public string Country { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
}

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

// --- VAT rates ---

/// <summary>
/// One VAT rate for one fiscal region — mainland Portugal and the two autonomous regions each set
/// their own percentage for the same rate tier, so the pair is what identifies a row, not the code
/// alone.
/// </summary>
public sealed record VatRateDto(
    Guid Id,
    string FiscalRegion,
    string Code,
    string Label,
    decimal Percentage,
    bool IsActive);

public sealed record UpdateVatRateRequest(decimal Percentage, bool IsActive);
