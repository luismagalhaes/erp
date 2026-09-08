using Erp.Purchasing.Domain;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface IGoodsReceiptStorage
{
    Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The receipt one of its lines belongs to, with its row locked. Invoicing reads how much of a
    /// line is left, and without the lock two invoices would each see the same room.
    /// </summary>
    Task<GoodsReceipt?> GetForUpdateByLineAsync(Guid receiptLineId, CancellationToken cancellationToken = default);

    Task<int> GetLastSequenceAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    Task<bool> NumberExistsAsync(Guid companyId, string number, CancellationToken cancellationToken = default);

    Task AddAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
}
