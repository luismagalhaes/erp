using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface ICompanyAdminService
{
    Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
