using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IProductFamilyService
{
    Task<IReadOnlyList<ProductFamilyDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<ProductFamilyDto> Query(Guid companyId);

    Task<ProductFamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the company already uses this code. Lets the host skip taken codes when numbering.</summary>
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task<ProductFamilyDto> CreateAsync(CreateProductFamilyRequest request, CancellationToken cancellationToken = default);
    Task<ProductFamilyDto?> UpdateAsync(Guid id, UpdateProductFamilyRequest request, CancellationToken cancellationToken = default);
}
