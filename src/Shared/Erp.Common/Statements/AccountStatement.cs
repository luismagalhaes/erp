namespace Erp.Common.Statements;

/// <summary>
/// A current account statement for one party over a period. A positive balance is always money
/// outstanding between us — owed to us by a customer, or owed by us to a supplier.
/// </summary>
/// <param name="OpeningBalance">The balance of everything before <paramref name="StartDate"/>.</param>
public sealed record AccountStatement(
    string PartyName,
    string PartyTaxId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal OpeningBalance,
    IReadOnlyList<AccountStatementEntry> Entries,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance);
