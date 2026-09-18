using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface ISupplierPaymentStorage
{
    Task<IReadOnlyList<SupplierPayment>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The listing shape, left open so the grid can filter, sort and page in the database.</summary>
    IQueryable<SupplierPaymentListItemDto> Query(Guid companyId);

    Task<SupplierPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each of those documents is already paid by payments that are not voided.
    /// Documents with nothing paid are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetPaidAmountsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(SupplierPayment payment, CancellationToken cancellationToken = default);
}
