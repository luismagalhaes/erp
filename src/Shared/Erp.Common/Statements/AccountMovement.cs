namespace Erp.Common.Statements;

/// <summary>
/// One document as it moves a current account: what it charged or what it took off. Amounts are
/// never negative — the side they sit on carries the sign.
/// </summary>
/// <param name="Source">One of <see cref="Constants.StatementSources"/>.</param>
/// <param name="SourceId">The document, so the statement can open it.</param>
/// <param name="Order">
/// Breaks ties between movements of the same date: documents before the payments that settle them.
/// </param>
public sealed record AccountMovement(
    DateOnly Date,
    string Source,
    Guid SourceId,
    string DocumentType,
    string DocumentNumber,
    decimal Debit,
    decimal Credit,
    DateOnly? DueDate = null,
    string? Description = null,
    int Order = 0);
