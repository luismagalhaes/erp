using Erp.FiscalPT.Documents;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Storage;

public sealed class SelfBilledInvoiceStorage(ErpDbContext dbContext) : ISelfBilledInvoiceStorage
{
    public async Task<IReadOnlyList<SelfBilledInvoice>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SelfBilledInvoice>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<SelfBilledInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<SelfBilledInvoice>()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SelfBilledInvoice>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? supplierTaxId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SelfBilledInvoice>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummaries)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId && x.IssueDate >= startDate && x.IssueDate <= endDate)
            .Where(x => supplierTaxId == null || x.Supplier.TaxId == supplierTaxId)
            // Ordered by series and sequence, because that is the order the file has to be read in.
            .OrderBy(x => x.SeriesId)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.Set<SelfBilledInvoice>()
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetSelfBilledQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiptLineIds);

        if (receiptLineIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var rows = await dbContext.Set<SelfBilledInvoiceLine>()
            .AsNoTracking()
            .Where(line => line.ReceiptLineId != null && receiptLineIds.Contains(line.ReceiptLineId.Value))
            // A voided document bills nothing, so what it took goes back on the shelf. Voiding is
            // the only status change these documents ever get, and it is final, so its presence is
            // the whole answer — no need to find the latest one.
            .Where(line => !line.Invoice.StatusChanges.Any(change => change.NewStatus == DocumentStatuses.Voided))
            .GroupBy(line => line.ReceiptLineId!.Value)
            .Select(group => new { ReceiptLineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ReceiptLineId, row => row.Quantity);
    }

    public async Task AddAsync(SelfBilledInvoice invoice, CancellationToken cancellationToken = default) =>
        await dbContext.Set<SelfBilledInvoice>().AddAsync(invoice, cancellationToken);

    public async Task AddStatusChangeAsync(
        SelfBilledInvoiceStatusChange change,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<SelfBilledInvoiceStatusChange>().AddAsync(change, cancellationToken);
}
