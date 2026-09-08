using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface IGoodsReceiptStorage
{
    Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing projection of a company's receipts, left as a query so the database applies the
    /// grid's filtering, sorting and paging.
    /// </summary>
    IQueryable<GoodsReceiptListItemDto> Query(Guid companyId);

    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The receipt one of its lines belongs to, with its row locked. Invoicing reads how much of a
    /// line is left, and without the lock two invoices would each see the same room.
    /// </summary>
    Task<GoodsReceipt?> GetForUpdateByLineAsync(Guid receiptLineId, CancellationToken cancellationToken = default);

    Task AddAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
}
