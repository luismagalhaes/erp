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
}
