namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>Net sales of one year against another, month by month and in total.</summary>
/// <param name="Months">Always twelve rows, January to December.</param>
public sealed record YearComparisonDto(
    int CurrentYear,
    int PreviousYear,
    IReadOnlyList<MonthComparisonDto> Months,
    decimal CurrentNetTotal,
    decimal PreviousNetTotal,
    decimal Difference,
    decimal? ChangePercent);
