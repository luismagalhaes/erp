using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface ICompanyAdminService
{
    Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CompanyDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CompanyDetailDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);

    Task<CompanyDetailDto?> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
}
