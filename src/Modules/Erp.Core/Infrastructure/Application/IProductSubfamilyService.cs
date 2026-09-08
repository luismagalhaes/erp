using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IProductSubfamilyService
{
    Task<IReadOnlyList<ProductSubfamilyDto>> GetAllAsync(Guid companyId, Guid? familyId = null, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<ProductSubfamilyDto> Query(Guid companyId);

    Task<ProductSubfamilyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductSubfamilyDto> CreateAsync(CreateProductSubfamilyRequest request, CancellationToken cancellationToken = default);
    Task<ProductSubfamilyDto?> UpdateAsync(Guid id, UpdateProductSubfamilyRequest request, CancellationToken cancellationToken = default);
}
