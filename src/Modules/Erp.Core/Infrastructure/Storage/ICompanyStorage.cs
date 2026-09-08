using Erp.Core.Domain;

namespace Erp.Core.Infrastructure.Storage;

public interface ICompanyStorage
{
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TaxIdExistsAsync(string taxId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
