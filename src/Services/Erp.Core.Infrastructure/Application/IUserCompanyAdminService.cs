using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IUserCompanyAdminService
{
    /// <param name="companyId">Limits the result to one company; null returns every membership.</param>
    Task<IReadOnlyList<UserCompanyAdminDto>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default);
    Task<UserCompanyAdminDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserCompanyAdminDto> CreateAsync(CreateUserCompanyRequest request, CancellationToken cancellationToken = default);
    Task<UserCompanyAdminDto?> UpdateAsync(Guid id, UpdateUserCompanyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
