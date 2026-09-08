using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<BrandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<BrandDto?> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
}

public interface IProductFamilyService
{
    Task<IReadOnlyList<ProductFamilyDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<ProductFamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductFamilyDto> CreateAsync(CreateProductFamilyRequest request, CancellationToken cancellationToken = default);
    Task<ProductFamilyDto?> UpdateAsync(Guid id, UpdateProductFamilyRequest request, CancellationToken cancellationToken = default);
}

public interface IProductSubfamilyService
{
    Task<IReadOnlyList<ProductSubfamilyDto>> GetAllAsync(Guid companyId, Guid? familyId = null, CancellationToken cancellationToken = default);
    Task<ProductSubfamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductSubfamilyDto> CreateAsync(CreateProductSubfamilyRequest request, CancellationToken cancellationToken = default);
    Task<ProductSubfamilyDto?> UpdateAsync(Guid id, UpdateProductSubfamilyRequest request, CancellationToken cancellationToken = default);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductListItemDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
}

public interface ICustomerService
{
    Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default);
}

public interface ISupplierService
{
    Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default);
}
