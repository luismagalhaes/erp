namespace Erp.Common.Statements;

/// <summary>
/// Turns a party's movements into a statement: the balance before the period, each movement in
/// the period with the running balance, and the totals. The same arithmetic serves customers and
/// suppliers; only the side that makes the balance grow changes.
/// </summary>
public static class AccountStatementBuilder
{
    public static AccountStatement Build(
        string partyName,
        string partyTaxId,
        AccountBalanceSide balanceSide,
        IEnumerable<AccountMovement> movements,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        ArgumentNullException.ThrowIfNull(movements);

        if (startDate is not null && endDate is not null && startDate > endDate)
            throw new ArgumentException("The statement period ends before it starts.", nameof(endDate));

        var ordered = movements
            .Where(movement => endDate is null || movement.Date <= endDate)
            .OrderBy(movement => movement.Date)
            .ThenBy(movement => movement.Order)
            .ThenBy(movement => movement.DocumentNumber, StringComparer.Ordinal)
            .ToList();

        var opening = ordered
            .Where(movement => startDate is not null && movement.Date < startDate)
            .Sum(movement => Signed(movement, balanceSide));

        var balance = opening;
        var entries = new List<AccountStatementEntry>();

        foreach (var movement in ordered.Where(movement => startDate is null || movement.Date >= startDate))
        {
            balance += Signed(movement, balanceSide);

            entries.Add(new AccountStatementEntry(
                movement.Date,
                movement.Source,
                movement.SourceId,
                movement.DocumentType,
                movement.DocumentNumber,
                movement.DueDate,
                movement.Description,
                movement.Debit,
                movement.Credit,
                balance));
        }

        return new AccountStatement(
            partyName,
            partyTaxId,
            startDate,
            endDate,
            opening,
            entries,
            entries.Sum(entry => entry.Debit),
            entries.Sum(entry => entry.Credit),
            balance);
    }

    private static decimal Signed(AccountMovement movement, AccountBalanceSide side) =>
        side == AccountBalanceSide.Debit
            ? movement.Debit - movement.Credit
            : movement.Credit - movement.Debit;
}
