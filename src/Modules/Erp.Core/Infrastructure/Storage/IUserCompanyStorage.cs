using Erp.Core.Domain;

namespace Erp.Core.Infrastructure.Storage;

public interface IUserCompanyStorage
{
    Task<IReadOnlyList<UserCompany>> GetActiveByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserCompany?> GetActiveAsync(string userId, Guid companyId, CancellationToken cancellationToken = default);
    /// <param name="companyId">Limits the result to one company; null returns every membership.</param>
    Task<IReadOnlyList<UserCompany>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default);
    Task<UserCompany?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddAsync(UserCompany userCompany, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void Remove(UserCompany userCompany);
}
