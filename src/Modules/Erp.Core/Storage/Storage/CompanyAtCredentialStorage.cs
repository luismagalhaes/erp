using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class CompanyAtCredentialStorage(AppDbContext dbContext) : ICompanyAtCredentialStorage
{
    public Task<CompanyAtCredential?> GetAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Set<CompanyAtCredential>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

    public async Task UpsertAsync(CompanyAtCredential credential, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Set<CompanyAtCredential>()
            .FirstOrDefaultAsync(x => x.CompanyId == credential.CompanyId, cancellationToken);

        if (existing is null)
        {
            await dbContext.Set<CompanyAtCredential>().AddAsync(credential, cancellationToken);
            return;
        }

        existing.SubUserId = credential.SubUserId;
        existing.ProtectedPassword = credential.ProtectedPassword;
        existing.UpdatedAtUtc = credential.UpdatedAtUtc;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
