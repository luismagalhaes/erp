using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface ISelfBilledInvoiceStorage
{
    Task<IReadOnlyList<SelfBilledInvoice>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The listing shape, left open so the grid can filter, sort and page in the database.</summary>
    IQueryable<SelfBilledInvoiceListItemDto> Query(Guid companyId);

    Task<SelfBilledInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The documents of a period, for the SAF-T. <paramref name="supplierTaxId"/> narrows it to one
    /// supplier, which is how the <c>"S"</c> file is produced: one per supplier, never one for all.
    /// </summary>
    Task<IReadOnlyList<SelfBilledInvoice>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? supplierTaxId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signature of the last document issued in the series, empty when it is the first. Read inside
    /// the issuing transaction, with the series row already locked.
    /// </summary>
    Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default);

    /// <summary>How much of each goods receipt line has already been self-billed.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetSelfBilledQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(SelfBilledInvoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a status change. The document header is never updated, which is what lets UPDATE be
    /// denied on its table.
    /// </summary>
    Task AddStatusChangeAsync(SelfBilledInvoiceStatusChange change, CancellationToken cancellationToken = default);
}
