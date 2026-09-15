namespace Erp.Main.Models.Core;

// --- Classification ---

public sealed record Brand(Guid Id, string Code, string Name, bool IsActive);

public sealed record CreateBrandRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateBrandRequest(string Name, bool IsActive);

public sealed record ProductFamily(Guid Id, string Code, string Name, bool IsActive, int SubfamilyCount);

public sealed record CreateProductFamilyRequest(Guid CompanyId, string Code, string Name);

public sealed record UpdateProductFamilyRequest(string Name, bool IsActive);

public sealed record ProductSubfamily(
    Guid Id,
    Guid FamilyId,
    string FamilyName,
    string Code,
    string Name,
    bool IsActive);

public sealed record CreateProductSubfamilyRequest(Guid CompanyId, Guid FamilyId, string Code, string Name);

public sealed record UpdateProductSubfamilyRequest(Guid FamilyId, string Name, bool IsActive);

// --- Products ---

public sealed record ProductListItem(
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

/// <summary>
/// ProductCategory of the inventory communication, for the product editor. A different vocabulary
/// from the SAF-T ProductType, which is why they are two fields and not one.
/// </summary>
public static class InventoryCategories
{
    public static readonly (string Code, string Label)[] All =
    [
        ("M", "Mercadorias"),
        ("P", "Produtos acabados e intermédios"),
        ("A", "Matérias-primas, subsidiárias e de consumo"),
        ("S", "Subprodutos, desperdícios e refugos"),
        ("T", "Produtos e trabalhos em curso"),
        ("B", "Ativos biológicos")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(category => category.Code == code).Label ?? code;
}

// --- Customers and suppliers ---

public sealed record Partner(
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

// --- VAT rates ---

public sealed record VatRate(Guid Id, string FiscalRegion, string Code, string Label, decimal Percentage, bool IsActive);

public sealed record UpdateVatRateRequest(decimal Percentage, bool IsActive);

/// <summary>PT (mainland), PT-AC (Açores) and PT-MA (Madeira) — the three fiscal regions VAT rates are set for.</summary>
public static class FiscalRegions
{
    public static readonly (string Code, string Name)[] All =
    [
        ("PT", "Portugal Continental"),
        ("PT-AC", "Açores"),
        ("PT-MA", "Madeira")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(region => region.Code == code).Name ?? code;
}
