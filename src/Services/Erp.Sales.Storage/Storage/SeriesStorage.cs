using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Storage;

public sealed class SeriesStorage(SalesDbContext dbContext) : ISeriesStorage
{
    public async Task<IReadOnlyList<Series>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Series
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.SeriesCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Series?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Series.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Takes an update lock on the series row for the rest of the transaction. This is what
    /// serializes issuing: a second transaction blocks here instead of reading the same
    /// sequence number and the same previous hash.
    /// </summary>
    public async Task<Series?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Series
            .FromSql($"SELECT * FROM [sales].[Series] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid companyId, string documentType, string seriesCode, CancellationToken cancellationToken = default)
    {
        return await dbContext.Series
            .AsNoTracking()
            .AnyAsync(
                x => x.CompanyId == companyId
                     && x.DocumentType == documentType
                     && x.SeriesCode == seriesCode,
                cancellationToken);
    }

    public async Task AddAsync(Series series, CancellationToken cancellationToken = default)
    {
        await dbContext.Series.AddAsync(series, cancellationToken);
    }
}
