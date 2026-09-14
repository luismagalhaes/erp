using Erp.FiscalPT.Documents;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.SeriesRegistry.Storage;

public sealed class SeriesStorage(AppDbContext dbContext) : ISeriesStorage
{
    public async Task<IReadOnlyList<Domain.Series>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Domain.Series>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.DocumentType)
            .ThenBy(x => x.SeriesCode)
            .ToListAsync(cancellationToken);
    }

    /// <remarks>
    /// The status, the stock effect and the right to issue are spelled out as expressions rather
    /// than read off the domain object, because the grid filters and sorts on them and only what
    /// the database can evaluate may take part in that.
    /// </remarks>
    public IQueryable<SeriesListItemDto> Query(Guid companyId)
    {
        return dbContext.Set<Domain.Series>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new SeriesListItemDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                DocumentType = x.DocumentType,
                SeriesCode = x.SeriesCode,
                CurrentSequence = x.CurrentSequence,
                ValidationCode = x.ValidationCode,
                // A switch expression can't be used here: this lambda compiles to an expression
                // tree for EF Core to translate to SQL, and the C# compiler refuses a switch
                // expression inside one (CS8514). The nested ternary is EF-translatable, so it stays.
#pragma warning disable S3358
                Status = x.Status == SeriesStatus.Created ? "Created"
                    : x.Status == SeriesStatus.Communicated ? "Communicated"
                    : x.Status == SeriesStatus.Active ? "Active"
                    : "Finalized",
                CanIssue = (x.Status == SeriesStatus.Communicated || x.Status == SeriesStatus.Active)
                    && x.ValidationCode != null
                    && x.ValidationCode != "",
                StockEffect = x.StockEffect == StockEffect.In ? "In"
                    : x.StockEffect == StockEffect.Out ? "Out"
                    : "None",
#pragma warning restore S3358
                SelfBilling = x.SelfBilling
            });
    }

    public async Task<Domain.Series?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Domain.Series>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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
    public async Task<Domain.Series?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // EF1002: the only thing interpolated is the table name, and it comes from the EF model,
        // never from a caller. The id is passed as parameter {0}, so it is parameterized as usual.
        return await dbContext.Set<Domain.Series>()
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<Domain.Series>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid companyId, string documentType, string seriesCode, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Domain.Series>()
            .AsNoTracking()
            .AnyAsync(
                x => x.CompanyId == companyId
                     && x.DocumentType == documentType
                     && x.SeriesCode == seriesCode,
                cancellationToken);
    }

    public async Task AddAsync(Domain.Series series, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Domain.Series>().AddAsync(series, cancellationToken);
    }
}
