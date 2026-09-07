using Erp.Purchasing.Domain;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface ISupplierReturnStorage
{
    Task<IReadOnlyList<SupplierReturn>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<SupplierReturn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each receipt line has already gone back. Derived from the return lines, so the
    /// receipt never has to be rewritten. Voided returns do not count — they sent nothing back.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetReturnedQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        CancellationToken cancellationToken = default);

    Task<int> GetLastSequenceAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    Task<bool> NumberExistsAsync(Guid companyId, string number, CancellationToken cancellationToken = default);

    Task AddAsync(SupplierReturn supplierReturn, CancellationToken cancellationToken = default);
}
