using Erp.Core.Domain;

namespace Erp.Core.Infrastructure.Storage;

public interface ICompanyAtCredentialStorage
{
    Task<CompanyAtCredential?> GetAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Inserts the company's first credential, or replaces the one it already had.</summary>
    Task UpsertAsync(CompanyAtCredential credential, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
