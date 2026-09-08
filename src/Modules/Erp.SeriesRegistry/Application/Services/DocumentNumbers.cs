using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.SeriesRegistry.Application.Services;

public sealed class DocumentNumbers(IDocumentCounterStorage storage) : IDocumentNumbers
{
    public async Task<string> NextAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var normalised = prefix.Trim().ToUpperInvariant();

        var counter = await storage.GetForUpdateAsync(companyId, normalised, year, cancellationToken);

        if (counter is null)
        {
            // The first document of the year. The lock that the reading took covers the key range,
            // so a second request asking at the same moment waits here rather than starting a
            // counter of its own.
            counter = DocumentCounter.Start(companyId, normalised, year);
            await storage.AddAsync(counter, cancellationToken);
        }

        return DocumentCounter.Format(normalised, year, counter.TakeNext());
    }
}
