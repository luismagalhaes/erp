using Erp.SeriesRegistry.Domain;

namespace Erp.SeriesRegistry.Infrastructure.Storage;

public interface IDocumentCounterStorage
{
    /// <summary>
    /// The counter for a company, prefix and year, with its row locked. Returns null when this is
    /// the first document of the year and there is nothing to count from yet.
    /// </summary>
    /// <remarks>
    /// Must be called inside a transaction: a lock taken by a bare statement is released as soon as
    /// that statement ends, long before the number it produced is written anywhere.
    /// </remarks>
    Task<DocumentCounter?> GetForUpdateAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default);

    Task AddAsync(DocumentCounter counter, CancellationToken cancellationToken = default);
}
