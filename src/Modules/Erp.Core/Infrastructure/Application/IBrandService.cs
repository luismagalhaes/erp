using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<BrandDto> Query(Guid companyId);

    Task<BrandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the company already uses this code. Lets the host skip taken codes when numbering.</summary>
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<BrandDto?> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
}
