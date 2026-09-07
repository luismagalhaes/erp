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
    /// <remarks>
    /// The one place in this module that drops to SQL, and not because of the query — EF has no
    /// way to express a lock hint, and <c>WITH (UPDLOCK, ROWLOCK)</c> is the whole point of the
    /// statement. The id still goes through as a parameter, and the table name is read from the
    /// mapping so it cannot drift away from where the entity actually lives.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.")]
    public async Task<Series?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // EF1002: the only thing interpolated is the table name, and it comes from the EF model,
        // never from a caller. The id is passed as parameter {0}, so it is parameterized as usual.
        return await dbContext.Series
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<Series>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                id)
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
