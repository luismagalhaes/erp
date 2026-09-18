namespace Erp.Common.Statements;

/// <summary>A line of the statement: the movement and the balance right after it.</summary>
public sealed record AccountStatementEntry(
    DateOnly Date,
    string Source,
    Guid SourceId,
    string DocumentType,
    string DocumentNumber,
    DateOnly? DueDate,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);
