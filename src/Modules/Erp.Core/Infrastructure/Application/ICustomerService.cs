using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface ICustomerService
{
    Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<PartnerDto> Query(Guid companyId);
    Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the company already uses this code. Lets the host skip taken codes when numbering.</summary>
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default);
}
