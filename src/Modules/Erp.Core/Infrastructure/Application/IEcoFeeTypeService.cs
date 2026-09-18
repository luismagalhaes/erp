using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IEcoFeeTypeService
{
    Task<IReadOnlyList<EcoFeeTypeDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<EcoFeeTypeDto> Query(Guid companyId);

    Task<EcoFeeTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EcoFeeTypeDto> CreateAsync(CreateEcoFeeTypeRequest request, CancellationToken cancellationToken = default);
    Task<EcoFeeTypeDto?> UpdateAsync(Guid id, UpdateEcoFeeTypeRequest request, CancellationToken cancellationToken = default);
}
