using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Core.Storage.Storage;

public sealed class VatRateStorage(AppDbContext dbContext) : IVatRateStorage
{
    public async Task<IReadOnlyList<VatRate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Set<VatRate>()
            .AsNoTracking()
            .OrderBy(x => x.FiscalRegion)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public IQueryable<VatRateDto> Query() =>
        dbContext.Set<VatRate>()
            .AsNoTracking()
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new VatRateDto
            {
                Id = x.Id,
                FiscalRegion = x.FiscalRegion,
                Code = x.Code,
                Label = x.Label,
                Percentage = x.Percentage,
                IsActive = x.IsActive
            });

    public Task<VatRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<VatRate>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
