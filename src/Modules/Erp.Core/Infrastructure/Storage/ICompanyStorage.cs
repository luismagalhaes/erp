using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Storage;

public interface ICompanyStorage
{
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<CompanyListItemDto> Query();

    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TaxIdExistsAsync(string taxId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
