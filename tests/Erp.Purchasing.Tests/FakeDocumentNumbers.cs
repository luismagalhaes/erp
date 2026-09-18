using Erp.SeriesRegistry.Infrastructure.Application;

namespace Erp.Purchasing.Tests;

/// <summary>
/// Hands out sequential numbers without a database.
/// </summary>
/// <remarks>
/// Counting in memory is enough for these tests: what they check is that a document carries the
/// number it was given, not that the counter is safe under concurrency — which is a question only a
/// real database can answer, and is asked in <c>Erp.IntegrationTests</c>.
/// </remarks>
public sealed class FakeDocumentNumbers : IDocumentNumbers
{
    private readonly Dictionary<(Guid Company, string Prefix, int Year), int> _counters = [];

    public Task<string> NextAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        var key = (companyId, prefix, year);

        _counters.TryGetValue(key, out var last);
        _counters[key] = ++last;

        return Task.FromResult($"{prefix}{year}/{last}");
    }

    public Task<int> NextSequenceAsync(
        Guid companyId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var counter = (companyId, key, 0);

        _counters.TryGetValue(counter, out var last);
        _counters[counter] = ++last;

        return Task.FromResult(last);
    }
}
