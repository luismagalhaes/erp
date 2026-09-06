using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class CompanyStorage(CoreDbContext dbContext) : ICompanyStorage
{
    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Companies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}
