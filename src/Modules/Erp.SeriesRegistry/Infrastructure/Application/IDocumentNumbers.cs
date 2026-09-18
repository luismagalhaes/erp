namespace Erp.SeriesRegistry.Infrastructure.Application;

/// <summary>
/// Hands out the internal document numbers — <c>REC2026/7</c> and its like — in sequence.
/// </summary>
/// <remarks>
/// Call it inside the transaction that writes the document. The number is only reserved for as long
/// as that transaction holds the counter row, and a transaction that rolls back gives its number
/// back, which is what keeps the sequence without holes.
/// </remarks>
public interface IDocumentNumbers
{
    Task<string> NextAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The next plain number of a counter that never restarts — 1, 2, 3 — for codes that carry no
    /// year, such as the codes of customers, suppliers and catalogue entries. Same locking rules as
    /// <see cref="NextAsync"/>: call it inside the transaction that writes what it numbers.
    /// </summary>
    Task<int> NextSequenceAsync(
        Guid companyId,
        string key,
        CancellationToken cancellationToken = default);
}
