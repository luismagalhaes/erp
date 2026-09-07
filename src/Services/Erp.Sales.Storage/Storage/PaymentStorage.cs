using Erp.FiscalPT.Documents;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Storage;

public sealed class PaymentStorage(SalesDbContext dbContext) : IPaymentStorage
{
    public async Task<IReadOnlyList<Payment>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .Include(x => x.Lines)
            .Include(x => x.Methods)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.Methods)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId && x.TransactionDate >= startDate && x.TransactionDate <= endDate)
            .OrderBy(x => x.SeriesId)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Signature of the last receipt of the series. Safe to read without extra locking because
    /// the caller already holds the update lock on the series row.
    /// </summary>
    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.Payments
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetSettledAmountsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        // A voided receipt settles nothing, so the invoices it touched go back to being owed.
        // The status lives in the status-change table, so "voided" is "has a change to A".
        var settled = await dbContext.PaymentLines
            .AsNoTracking()
            .Where(line => documentIds.Contains(line.OriginatingDocumentId))
            .Where(line => !dbContext.PaymentStatusChanges
                .Any(change => change.PaymentId == line.PaymentId
                               && change.NewStatus == DocumentStatuses.Voided))
            .GroupBy(line => line.OriginatingDocumentId)
            .Select(group => new { DocumentId = group.Key, Amount = group.Sum(line => line.AppliedAmount) })
            .ToListAsync(cancellationToken);

        return settled.ToDictionary(x => x.DocumentId, x => x.Amount);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task AddStatusChangeAsync(PaymentStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        await dbContext.PaymentStatusChanges.AddAsync(statusChange, cancellationToken);
    }
}
