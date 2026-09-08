using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class UserCompanyService(IUserCompanyStorage storage) : IUserCompanyService
{
    public async Task<IReadOnlyList<UserCompanyDto>> GetUserCompaniesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var userCompanies = await storage.GetActiveByUserAsync(userId, cancellationToken);

        return userCompanies
            .Select(x => new UserCompanyDto(x.CompanyId, x.Company.Name, x.Role))
            .ToList();
    }

    public async Task<string?> GetUserRoleAsync(string userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || companyId == Guid.Empty)
            return null;

        var userCompany = await storage.GetActiveAsync(userId, companyId, cancellationToken);
        return userCompany?.Role;
    }

    public async Task<bool> HasRoleAsync(string userId, Guid companyId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var currentRole = await GetUserRoleAsync(userId, companyId, cancellationToken);
        return string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> CanAccessCompanyAsync(string userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || companyId == Guid.Empty)
            return false;

        return await storage.GetActiveAsync(userId, companyId, cancellationToken) is not null;
    }
}
