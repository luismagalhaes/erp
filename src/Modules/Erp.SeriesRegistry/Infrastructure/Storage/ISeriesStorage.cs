using Erp.SeriesRegistry.Domain;

namespace Erp.SeriesRegistry.Infrastructure.Storage;

public interface ISeriesStorage
{
    Task<IReadOnlyList<Domain.Series>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Domain.Series?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a series taking an update lock on the row, so that two concurrent issuing
    /// transactions cannot read the same sequence number or the same previous hash.
    /// Must be called inside a transaction.
    /// </summary>
    Task<Domain.Series?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid companyId, string documentType, string seriesCode, CancellationToken cancellationToken = default);

    Task AddAsync(Domain.Series series, CancellationToken cancellationToken = default);
}
