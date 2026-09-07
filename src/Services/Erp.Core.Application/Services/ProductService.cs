using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.FiscalPT.Inventory;

namespace Erp.Core.Application.Services;

public sealed class ProductService(
    IProductStorage storage,
    IProductFamilyStorage familyStorage,
    IProductSubfamilyStorage subfamilyStorage,
    IBrandStorage brandStorage) : IProductService
{
    private static readonly string[] ProductTypes = ["P", "S", "O", "I"];

    /// <summary>
    /// The inventory categories, taken from the valued schema so the full set is available. A
    /// product classified B on the older schema falls back to M when the file is written, which is
    /// the generator's business rather than the product file's.
    /// </summary>
    private static readonly string[] InventoryCategories =
        InventoryConstants.ProductCategories(InventoryFileVersion.Valued);

    private static readonly string[] TaxCodes = ["RED", "INT", "NOR", "ISE"];
    private static readonly string[] TaxCountryRegions = ["PT", "PT-AC", "PT-MA"];

    public async Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var products = await storage.GetAllAsync(companyId, cancellationToken);
        return products.Select(Map).ToList();
    }

    public async Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await storage.GetByIdAsync(id, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<ProductListItemDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);

        Validate(
            request.ProductType,
            request.InventoryCategory,
            request.DefaultTaxCode,
            request.DefaultTaxCountryRegion,
            request.UnitPrice);

        await ValidateClassificationAsync(request.FamilyId, request.SubfamilyId, request.BrandId, cancellationToken);

        var productCode = request.ProductCode.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, productCode, cancellationToken))
            throw new InvalidOperationException($"Product code '{productCode}' already exists for this company.");

        var product = new Product
        {
            CompanyId = request.CompanyId,
            ProductCode = productCode,
            Description = request.Description.Trim(),
            ProductType = request.ProductType,
            InventoryCategory = request.InventoryCategory,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            UnitCost = request.UnitCost,
            DefaultTaxCountryRegion = request.DefaultTaxCountryRegion,
            DefaultTaxCode = request.DefaultTaxCode,
            DefaultTaxPercentage = request.DefaultTaxPercentage,
            Barcode = Normalize(request.Barcode),
            FamilyId = request.FamilyId,
            SubfamilyId = request.SubfamilyId,
            BrandId = request.BrandId
        };

        await storage.AddAsync(product, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(await storage.GetByIdAsync(product.Id, cancellationToken) ?? product);
    }

    public async Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);

        Validate(
            request.ProductType,
            request.InventoryCategory,
            request.DefaultTaxCode,
            request.DefaultTaxCountryRegion,
            request.UnitPrice);

        await ValidateClassificationAsync(request.FamilyId, request.SubfamilyId, request.BrandId, cancellationToken);

        var product = await storage.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        product.Description = request.Description.Trim();
        product.UnitPrice = request.UnitPrice;
        product.UnitCost = request.UnitCost;
        product.ProductType = request.ProductType;
        product.InventoryCategory = request.InventoryCategory;
        product.UnitOfMeasure = request.UnitOfMeasure;
        product.DefaultTaxCountryRegion = request.DefaultTaxCountryRegion;
        product.DefaultTaxCode = request.DefaultTaxCode;
        product.DefaultTaxPercentage = request.DefaultTaxPercentage;
        product.Barcode = Normalize(request.Barcode);
        product.FamilyId = request.FamilyId;
        product.SubfamilyId = request.SubfamilyId;
        product.BrandId = request.BrandId;
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(await storage.GetByIdAsync(id, cancellationToken) ?? product);
    }

    private static void Validate(
        string productType,
        string inventoryCategory,
        string taxCode,
        string taxCountryRegion,
        decimal unitPrice)
    {
        if (!ProductTypes.Contains(productType, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown product type '{productType}'.", nameof(productType));

        if (!InventoryCategories.Contains(inventoryCategory, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Unknown inventory category '{inventoryCategory}'.", nameof(inventoryCategory));
        }

        if (!TaxCodes.Contains(taxCode, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown tax code '{taxCode}'.", nameof(taxCode));

        if (!TaxCountryRegions.Contains(taxCountryRegion, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown tax country region '{taxCountryRegion}'.", nameof(taxCountryRegion));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
    }

    /// <summary>
    /// The classification is optional, but when it is given it has to exist, and the subfamily
    /// has to belong to the family that was chosen.
    /// </summary>
    private async Task ValidateClassificationAsync(
        Guid? familyId,
        Guid? subfamilyId,
        Guid? brandId,
        CancellationToken cancellationToken)
    {
        if (familyId is not null && await familyStorage.GetByIdAsync(familyId.Value, cancellationToken) is null)
            throw new ArgumentException("The family was not found.", nameof(familyId));

        if (brandId is not null && await brandStorage.GetByIdAsync(brandId.Value, cancellationToken) is null)
            throw new ArgumentException("The brand was not found.", nameof(brandId));

        if (subfamilyId is null)
            return;

        var subfamily = await subfamilyStorage.GetByIdAsync(subfamilyId.Value, cancellationToken)
            ?? throw new ArgumentException("The subfamily was not found.", nameof(subfamilyId));

        if (familyId is null)
            throw new ArgumentException("A subfamily requires the family it belongs to.", nameof(subfamilyId));

        if (subfamily.FamilyId != familyId)
            throw new ArgumentException("The subfamily does not belong to the chosen family.", nameof(subfamilyId));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductListItemDto Map(Product product) =>
        new(product.Id,
            product.ProductCode,
            product.Description,
            product.ProductType,
            product.UnitOfMeasure,
            product.UnitPrice,
            product.DefaultTaxCode,
            product.DefaultTaxPercentage,
            product.Barcode,
            product.FamilyId,
            product.Family?.Name,
            product.SubfamilyId,
            product.Subfamily?.Name,
            product.BrandId,
            product.Brand?.Name,
            product.IsActive,
            product.UnitCost,
            product.InventoryCategory);
}
