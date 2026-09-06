using Erp.FiscalPT.Documents;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

public sealed class ProductService(IProductStorage storage, ISalesUnitOfWork unitOfWork) : IProductService
{
    private static readonly string[] ProductTypes = ["P", "S", "O", "I"];

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

        Validate(request.ProductType, request.DefaultTaxCode, request.DefaultTaxCountryRegion, request.UnitPrice);

        var productCode = request.ProductCode.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, productCode, cancellationToken))
            throw new InvalidOperationException($"Product code '{productCode}' already exists for this company.");

        var product = new Product
        {
            CompanyId = request.CompanyId,
            ProductCode = productCode,
            Description = request.Description.Trim(),
            ProductType = request.ProductType,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            DefaultTaxCountryRegion = request.DefaultTaxCountryRegion,
            DefaultTaxCode = request.DefaultTaxCode,
            DefaultTaxPercentage = request.DefaultTaxPercentage
        };

        await storage.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(product);
    }

    public async Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);

        Validate(request.ProductType, request.DefaultTaxCode, request.DefaultTaxCountryRegion, request.UnitPrice);

        var product = await storage.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        product.Description = request.Description.Trim();
        product.UnitPrice = request.UnitPrice;
        product.ProductType = request.ProductType;
        product.UnitOfMeasure = request.UnitOfMeasure;
        product.DefaultTaxCountryRegion = request.DefaultTaxCountryRegion;
        product.DefaultTaxCode = request.DefaultTaxCode;
        product.DefaultTaxPercentage = request.DefaultTaxPercentage;
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(product);
    }

    private static void Validate(string productType, string taxCode, string taxCountryRegion, decimal unitPrice)
    {
        if (!ProductTypes.Contains(productType, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown product type '{productType}'.", nameof(productType));

        if (!TaxCodes.All.Contains(taxCode, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown tax code '{taxCode}'.", nameof(taxCode));

        if (!TaxCountryRegions.All.Contains(taxCountryRegion, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown tax country region '{taxCountryRegion}'.", nameof(taxCountryRegion));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
    }

    private static ProductListItemDto Map(Product product) =>
        new(product.Id,
            product.ProductCode,
            product.Description,
            product.ProductType,
            product.UnitOfMeasure,
            product.UnitPrice,
            product.DefaultTaxCode,
            product.DefaultTaxPercentage,
            product.IsActive);
}
