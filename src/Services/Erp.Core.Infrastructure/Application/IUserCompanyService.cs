using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IUserCompanyService
{
    Task<IReadOnlyList<UserCompanyDto>> GetUserCompaniesAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserRoleAsync(string userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAsync(string userId, Guid companyId, string role, CancellationToken cancellationToken = default);
    Task<bool> CanAccessCompanyAsync(string userId, Guid companyId, CancellationToken cancellationToken = default);
}
