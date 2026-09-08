using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface ISupplierReturnStorage
{
    Task<IReadOnlyList<SupplierReturn>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The listing shape, left open so the grid can filter, sort and page in the database.</summary>
    IQueryable<SupplierReturnListItemDto> Query(Guid companyId);

    Task<SupplierReturn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each receipt line has already gone back. Derived from the return lines, so the
    /// receipt never has to be rewritten. Voided returns do not count — they sent nothing back.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetReturnedQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(SupplierReturn supplierReturn, CancellationToken cancellationToken = default);
}
