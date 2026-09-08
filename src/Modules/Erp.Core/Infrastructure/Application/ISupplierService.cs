using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface ISupplierService
{
    Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<PartnerDto> Query(Guid companyId);
    Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default);
}
