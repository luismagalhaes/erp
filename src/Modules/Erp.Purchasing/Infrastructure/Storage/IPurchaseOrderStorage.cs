using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface IPurchaseOrderStorage
{
    Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing projection of a company's orders, left as a query so the database applies the
    /// grid's filtering, sorting and paging.
    /// </summary>
    IQueryable<PurchaseOrderListItemDto> Query(Guid companyId);

    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the order with its row locked, for the receipt to be registered against a quantity
    /// nobody else is changing at the same time.
    /// </summary>
    Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, found by one of its lines. A receipt knows which order line it is receiving, not
    /// which order that line belongs to.
    /// </summary>
    Task<PurchaseOrder?> GetForUpdateByLineAsync(Guid orderLineId, CancellationToken cancellationToken = default);

    /// <summary>Every open order line that still has a pending quantity.</summary>
    Task<IReadOnlyList<PurchaseOrder>> GetPendingAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Highest sequence used by an order of this company in this year, for the next number. The
    /// order number is ours and has no fiscal meaning, so a gap in it costs nothing.
    /// </summary>
    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
