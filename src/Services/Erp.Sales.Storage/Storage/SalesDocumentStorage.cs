using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Storage;

public sealed class SalesDocumentStorage(SalesDbContext dbContext) : ISalesDocumentStorage
{
    public async Task<IReadOnlyList<SalesDocument>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SalesDocuments
            .AsNoTracking()
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.SalesDocuments
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Signature of the last document of the series. Safe to read without extra locking because
    /// the caller already holds the update lock on the series row.
    /// </summary>
    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.SalesDocuments
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task AddAsync(SalesDocument document, CancellationToken cancellationToken = default)
    {
        await dbContext.SalesDocuments.AddAsync(document, cancellationToken);
    }

    public async Task AddStatusChangeAsync(DocumentStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        await dbContext.DocumentStatusChanges.AddAsync(statusChange, cancellationToken);
    }
}
