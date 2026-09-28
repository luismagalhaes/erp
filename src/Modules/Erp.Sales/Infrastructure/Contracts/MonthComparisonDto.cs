namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>One calendar month of a year, next to the same month of another year.</summary>
/// <param name="Difference">Current minus previous, in net sales.</param>
/// <param name="ChangePercent">
/// Difference over the previous year's value, in percent. Null when the previous year sold nothing
/// that month: a percentage of zero is not a number worth showing.
/// </param>
public sealed record MonthComparisonDto(
    int Month,
    decimal CurrentNetTotal,
    decimal PreviousNetTotal,
    decimal Difference,
    decimal? ChangePercent);
