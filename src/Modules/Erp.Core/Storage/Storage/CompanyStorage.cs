using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Core.Storage.Storage;

public sealed class CompanyStorage(AppDbContext dbContext) : ICompanyStorage
{
    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Company>()
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public IQueryable<CompanyListItemDto> Query() =>
        dbContext.Set<Company>()
            .AsNoTracking()
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new CompanyListItemDto
            {
                Id = x.Id,
                Name = x.Name,
                LegalName = x.LegalName,
                TaxId = x.TaxId,
                IsActive = x.IsActive
            });

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Company>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> TaxIdExistsAsync(string taxId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Company>()
            .AnyAsync(x => x.TaxId == taxId && (excludeId == null || x.Id != excludeId), cancellationToken);
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Company>().AddAsync(company, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
