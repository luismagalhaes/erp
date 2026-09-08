using Erp.FiscalPT.Documents;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Sales.Storage.Storage;

public sealed class SalesDocumentStorage(ErpDbContext dbContext) : ISalesDocumentStorage
{
    public async Task<IReadOnlyList<SalesDocument>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SalesDocument>()
            .AsNoTracking()
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SalesDocument>()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesDocument>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SalesDocument>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId && x.DocumentDate >= startDate && x.DocumentDate <= endDate)
            .OrderBy(x => x.SeriesId)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Locks the document row, then loads it. Two statements on purpose: the lock has to come from
    /// raw SQL, because EF has no way to express a hint, and the entity is needed with its status
    /// changes — which the locking statement alone would not bring. The lock is held by the
    /// transaction either way, so what matters is that it is taken first.
    /// </summary>
    public async Task<SalesDocument?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // EF1002: only the table name is interpolated, and it comes from the model. The id is
        // passed as parameter {0}.
#pragma warning disable EF1002
        var locked = await dbContext.Set<SalesDocument>()
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<SalesDocument>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                id)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002

        return locked is null ? null : await GetByIdAsync(id, cancellationToken);
    }

    public async Task<decimal> GetCreditedAmountAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        // A voided credit note credits nothing, so the value it carried goes back to the invoice.
        // The status lives in the status-change table, so "voided" is "has a change to A".
        return await dbContext.Set<SalesDocument>()
            .AsNoTracking()
            .Where(x => x.RectifiedDocumentId == documentId)
            .Where(x => x.DocumentType == SalesDocumentTypes.CreditNote)
            .Where(x => !dbContext.Set<DocumentStatusChange>()
                .Any(change => change.DocumentId == x.Id && change.NewStatus == DocumentStatuses.Voided))
            .SumAsync(x => x.GrossTotal, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        IReadOnlyCollection<Guid> movementLineIds,
        CancellationToken cancellationToken = default)
    {
        if (movementLineIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        // A voided invoice takes nothing from the movement, so its lines go back to pending.
        var invoiced = await dbContext.Set<SalesDocumentLine>()
            .AsNoTracking()
            .Where(line => line.OriginatingLineId != null && movementLineIds.Contains(line.OriginatingLineId.Value))
            .Where(line => !dbContext.Set<DocumentStatusChange>()
                .Any(change => change.DocumentId == line.DocumentId
                               && change.NewStatus == DocumentStatuses.Voided))
            .GroupBy(line => line.OriginatingLineId!.Value)
            .Select(group => new { LineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        return invoiced.ToDictionary(x => x.LineId, x => x.Quantity);
    }

    /// <summary>
    /// Signature of the last document of the series. Safe to read without extra locking because
    /// the caller already holds the update lock on the series row.
    /// </summary>
    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.Set<SalesDocument>()
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task AddAsync(SalesDocument document, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<SalesDocument>().AddAsync(document, cancellationToken);
    }

    public async Task AddStatusChangeAsync(DocumentStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<DocumentStatusChange>().AddAsync(statusChange, cancellationToken);
    }
}
