using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.SeriesRegistry.Storage;

public sealed class DocumentCounterStorage(ErpDbContext dbContext) : IDocumentCounterStorage
{
    public async Task<DocumentCounter?> GetForUpdateAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        // HOLDLOCK as well as UPDLOCK, because the row usually does not exist yet on the first
        // document of a year — and an update lock has nothing to hold when nothing matches. The
        // range lock is what makes two first requests queue up instead of both starting a counter.
        //
        // EF1002/EF1003: only the table name is interpolated, and it comes from the model. The
        // company, prefix and year are passed as parameters {0}, {1} and {2}.
#pragma warning disable EF1002, EF1003
        return await dbContext.Set<DocumentCounter>()
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<DocumentCounter>(dbContext)} WITH (UPDLOCK, HOLDLOCK, ROWLOCK) " +
                "WHERE [CompanyId] = {0} AND [Prefix] = {1} AND [Year] = {2}",
                companyId, prefix, year)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002, EF1003
    }

    public async Task AddAsync(DocumentCounter counter, CancellationToken cancellationToken = default) =>
        await dbContext.Set<DocumentCounter>().AddAsync(counter, cancellationToken);
}
