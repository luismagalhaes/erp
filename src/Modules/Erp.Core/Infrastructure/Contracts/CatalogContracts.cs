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
        string inventoryCategory = "M",
        string? defaultTaxExemptionCode = null,
        Guid? ecoFeeTypeId = null,
        string? ecoFeeTypeCode = null,
        decimal? ecoFeeWeightKg = null)
    {
        DefaultTaxExemptionCode = defaultTaxExemptionCode;
        EcoFeeTypeId = ecoFeeTypeId;
        EcoFeeTypeCode = ecoFeeTypeCode;
        EcoFeeWeightKg = ecoFeeWeightKg;
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

    /// <summary>Exemption reason from the AT table, for an article taxed at zero.</summary>
    public string? DefaultTaxExemptionCode { get; init; }

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

    /// <summary>The eco-fee this article carries (e.g. the battery or oil Ecovalor), if any.</summary>
    public Guid? EcoFeeTypeId { get; init; }

    public string? EcoFeeTypeCode { get; init; }

    /// <summary>Net weight in kilograms, used when the linked eco-fee is charged per kilogram.</summary>
    public decimal? EcoFeeWeightKg { get; init; }
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
    string InventoryCategory = "M",
    string? DefaultTaxExemptionCode = null,
    Guid? EcoFeeTypeId = null,
    decimal? EcoFeeWeightKg = null);

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
    string InventoryCategory = "M",
    string? DefaultTaxExemptionCode = null,
    Guid? EcoFeeTypeId = null,
    decimal? EcoFeeWeightKg = null);

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
/// alone. Init properties for the same reason as <see cref="BrandDto"/>: EF Core only maps members
/// over an object initializer, which is what keeps the OData options translatable to SQL.
/// </summary>
public sealed record VatRateDto
{
    public VatRateDto()
    {
    }

    public VatRateDto(Guid id, string fiscalRegion, string code, string label, decimal percentage, bool isActive)
    {
        Id = id;
        FiscalRegion = fiscalRegion;
        Code = code;
        Label = label;
        Percentage = percentage;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string FiscalRegion { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public decimal Percentage { get; init; }
    public bool IsActive { get; init; }
}

public sealed record UpdateVatRateRequest(decimal Percentage, bool IsActive);

// --- Eco-fees (Ecovalor) ---

/// <summary>
/// An eco-fee as the listings see it. Init properties for the same reason as <see cref="BrandDto"/>:
/// EF Core only maps members over an object initializer, which is what keeps the OData $filter and
/// $orderby applied afterwards translatable to SQL.
/// </summary>
public sealed record EcoFeeTypeDto
{
    public EcoFeeTypeDto()
    {
    }

    public EcoFeeTypeDto(
        Guid id,
        string code,
        string description,
        string calculationBasis,
        decimal rate,
        string managingEntityName,
        bool isActive)
    {
        Id = id;
        Code = code;
        Description = description;
        CalculationBasis = calculationBasis;
        Rate = rate;
        ManagingEntityName = managingEntityName;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>"PerUnit" or "PerKg" — the name of the <c>EcoFeeCalculationBasis</c> value.</summary>
    public string CalculationBasis { get; init; } = string.Empty;

    public decimal Rate { get; init; }
    public string ManagingEntityName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed record CreateEcoFeeTypeRequest(
    Guid CompanyId,
    string Code,
    string Description,
    string CalculationBasis,
    decimal Rate,
    string ManagingEntityName);

public sealed record UpdateEcoFeeTypeRequest(
    string Description,
    decimal Rate,
    string ManagingEntityName,
    bool IsActive);
