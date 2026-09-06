using Erp.Sales.Domain;

namespace Erp.Sales.Infrastructure.Storage;

public interface ISeriesStorage
{
    Task<IReadOnlyList<Series>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Series?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a series taking an update lock on the row, so that two concurrent issuing
    /// transactions cannot read the same sequence number or the same previous hash.
    /// Must be called inside a transaction.
    /// </summary>
    Task<Series?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid companyId, string documentType, string seriesCode, CancellationToken cancellationToken = default);

    Task AddAsync(Series series, CancellationToken cancellationToken = default);
}
