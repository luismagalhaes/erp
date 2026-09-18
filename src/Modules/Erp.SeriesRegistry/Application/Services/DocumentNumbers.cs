using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.SeriesRegistry.Application.Services;

public sealed class DocumentNumbers(IDocumentCounterStorage storage) : IDocumentNumbers
{
    /// <summary>The year a counter that never restarts is kept under.</summary>
    private const int NoYear = 0;

    public async Task<string> NextAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var normalised = prefix.Trim().ToUpperInvariant();
        var number = await TakeNextAsync(companyId, normalised, year, cancellationToken);

        return DocumentCounter.Format(normalised, year, number);
    }

    public Task<int> NextSequenceAsync(
        Guid companyId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return TakeNextAsync(companyId, key.Trim().ToUpperInvariant(), NoYear, cancellationToken);
    }

    private async Task<int> TakeNextAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken)
    {
        var counter = await storage.GetForUpdateAsync(companyId, prefix, year, cancellationToken);

        if (counter is null)
        {
            // The first number of this counter. The lock that the reading took covers the key
            // range, so a second request asking at the same moment waits here rather than starting
            // a counter of its own.
            counter = DocumentCounter.Start(companyId, prefix, year);
            await storage.AddAsync(counter, cancellationToken);
        }

        return counter.TakeNext();
    }
}
