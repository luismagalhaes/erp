using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class BrandService(IBrandStorage storage) : IBrandService
{
    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var brands = await storage.GetAllAsync(companyId, cancellationToken);
        return brands.Select(Map).ToList();
    }

    public IQueryable<BrandDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<BrandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var brand = await storage.GetByIdAsync(id, cancellationToken);
        return brand is null ? null : Map(brand);
    }

    public async Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Brand code '{code}' already exists for this company.");

        var brand = new Brand
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim()
        };

        await storage.AddAsync(brand, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(brand);
    }

    public async Task<BrandDto?> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var brand = await storage.GetByIdAsync(id, cancellationToken);
        if (brand is null)
            return null;

        brand.Name = request.Name.Trim();
        brand.IsActive = request.IsActive;
        brand.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(brand);
    }

    private static BrandDto Map(Brand brand) => new(brand.Id, brand.Code, brand.Name, brand.IsActive);
}

public sealed class VatRateService(IVatRateStorage storage) : IVatRateService
{
    public async Task<IReadOnlyList<VatRateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rates = await storage.GetAllAsync(cancellationToken);
        return rates.Select(Map).ToList();
    }

    public async Task<VatRateDto?> UpdateAsync(Guid id, UpdateVatRateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rate = await storage.GetByIdAsync(id, cancellationToken);
        if (rate is null)
            return null;

        rate.Percentage = request.Percentage;
        rate.IsActive = request.IsActive;
        rate.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(rate);
    }

    private static VatRateDto Map(VatRate rate) =>
        new(rate.Id, rate.FiscalRegion, rate.Code, rate.Label, rate.Percentage, rate.IsActive);
}

public sealed class ProductFamilyService(IProductFamilyStorage storage) : IProductFamilyService
{
    public async Task<IReadOnlyList<ProductFamilyDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var families = await storage.GetAllAsync(companyId, cancellationToken);
        return families.Select(Map).ToList();
    }

    public IQueryable<ProductFamilyDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<ProductFamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var family = await storage.GetByIdAsync(id, cancellationToken);
        return family is null ? null : Map(family);
    }

    public async Task<ProductFamilyDto> CreateAsync(CreateProductFamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Family code '{code}' already exists for this company.");

        var family = new ProductFamily
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim()
        };

        await storage.AddAsync(family, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(family);
    }

    public async Task<ProductFamilyDto?> UpdateAsync(Guid id, UpdateProductFamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var family = await storage.GetByIdAsync(id, cancellationToken);
        if (family is null)
            return null;

        family.Name = request.Name.Trim();
        family.IsActive = request.IsActive;
        family.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(family);
    }

    private static ProductFamilyDto Map(ProductFamily family) =>
        new(family.Id, family.Code, family.Name, family.IsActive, family.Subfamilies.Count);
}

public sealed class ProductSubfamilyService(
    IProductSubfamilyStorage storage,
    IProductFamilyStorage familyStorage) : IProductSubfamilyService
{
    public async Task<IReadOnlyList<ProductSubfamilyDto>> GetAllAsync(
        Guid companyId,
        Guid? familyId = null,
        CancellationToken cancellationToken = default)
    {
        var subfamilies = await storage.GetAllAsync(companyId, familyId, cancellationToken);
        return subfamilies.Select(Map).ToList();
    }

    public IQueryable<ProductSubfamilyDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<ProductSubfamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subfamily = await storage.GetByIdAsync(id, cancellationToken);
        return subfamily is null ? null : Map(subfamily);
    }

    public async Task<ProductSubfamilyDto> CreateAsync(CreateProductSubfamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var family = await familyStorage.GetByIdAsync(request.FamilyId, cancellationToken)
            ?? throw new ArgumentException("The family was not found.", nameof(request));

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Subfamily code '{code}' already exists for this company.");

        var subfamily = new ProductSubfamily
        {
            CompanyId = request.CompanyId,
            FamilyId = family.Id,
            Family = family,
            Code = code,
            Name = request.Name.Trim()
        };

        await storage.AddAsync(subfamily, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(subfamily);
    }

    public async Task<ProductSubfamilyDto?> UpdateAsync(Guid id, UpdateProductSubfamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var subfamily = await storage.GetByIdAsync(id, cancellationToken);
        if (subfamily is null)
            return null;

        if (subfamily.FamilyId != request.FamilyId)
        {
            subfamily.Family = await familyStorage.GetByIdAsync(request.FamilyId, cancellationToken)
                ?? throw new ArgumentException("The family was not found.", nameof(request));
            subfamily.FamilyId = request.FamilyId;
        }

        subfamily.Name = request.Name.Trim();
        subfamily.IsActive = request.IsActive;
        subfamily.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(subfamily);
    }

    private static ProductSubfamilyDto Map(ProductSubfamily subfamily) =>
        new(subfamily.Id,
            subfamily.FamilyId,
            subfamily.Family?.Name ?? string.Empty,
            subfamily.Code,
            subfamily.Name,
            subfamily.IsActive);
}
