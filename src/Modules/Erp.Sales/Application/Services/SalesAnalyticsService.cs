using Erp.FiscalPT.Documents;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

/// <summary>
/// Net sales as an accountant would read them: invoices, simplified invoices, invoice-receipts and
/// debit notes add up, credit notes take away, voided documents do not count at all.
/// </summary>
public sealed class SalesAnalyticsService(ISalesDocumentStorage documentStorage) : ISalesAnalyticsService
{
    public async Task<IReadOnlyList<MonthlySalesDto>> GetMonthlySalesAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("The end date is before the start date.", nameof(endDate));

        var figures = await documentStorage.GetSalesFiguresAsync(companyId, startDate, endDate, cancellationToken);

        var byMonth = figures
            .GroupBy(figure => (figure.DocumentDate.Year, figure.DocumentDate.Month))
            .ToDictionary(
                group => group.Key,
                group => new MonthlySalesDto(
                    group.Key.Year,
                    group.Key.Month,
                    group.Sum(Net),
                    group.Sum(Gross),
                    group.Count()));

        // Every month of the period is listed, including the empty ones: a gap in the series would
        // read as missing data, when it is a month with no sales.
        var months = new List<MonthlySalesDto>();

        for (var cursor = new DateOnly(startDate.Year, startDate.Month, 1);
             cursor <= endDate;
             cursor = cursor.AddMonths(1))
        {
            months.Add(byMonth.TryGetValue((cursor.Year, cursor.Month), out var month)
                ? month
                : new MonthlySalesDto(cursor.Year, cursor.Month, 0m, 0m, 0));
        }

        return months;
    }

    public async Task<YearComparisonDto> CompareYearsAsync(
        Guid companyId,
        int year,
        int previousYear,
        CancellationToken cancellationToken = default)
    {
        var current = await GetMonthlySalesAsync(
            companyId, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);

        var previous = await GetMonthlySalesAsync(
            companyId, new DateOnly(previousYear, 1, 1), new DateOnly(previousYear, 12, 31), cancellationToken);

        var months = current
            .Zip(previous, (now, before) => new MonthComparisonDto(
                now.Month,
                now.NetTotal,
                before.NetTotal,
                now.NetTotal - before.NetTotal,
                Percent(now.NetTotal - before.NetTotal, before.NetTotal)))
            .ToList();

        var currentTotal = current.Sum(month => month.NetTotal);
        var previousTotal = previous.Sum(month => month.NetTotal);

        return new YearComparisonDto(
            year,
            previousYear,
            months,
            currentTotal,
            previousTotal,
            currentTotal - previousTotal,
            Percent(currentTotal - previousTotal, previousTotal));
    }

    public async Task<IReadOnlyList<CustomerSalesDto>> GetTopCustomersAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("The end date is before the start date.", nameof(endDate));

        var figures = await documentStorage.GetSalesFiguresAsync(companyId, startDate, endDate, cancellationToken);

        return
        [
            .. figures
                .GroupBy(figure => figure.CustomerTaxId)
                .Select(group => new CustomerSalesDto(
                    group.First().CustomerName,
                    group.Key,
                    group.Sum(Net),
                    group.Count()))
                .OrderByDescending(customer => customer.NetTotal)
                .Take(Math.Max(count, 1))
        ];
    }

    private static decimal Net(SalesFigure figure) => Sign(figure) * figure.NetTotal;

    private static decimal Gross(SalesFigure figure) => Sign(figure) * figure.GrossTotal;

    private static int Sign(SalesFigure figure) =>
        string.Equals(figure.DocumentType, SalesDocumentTypes.CreditNote, StringComparison.Ordinal) ? -1 : 1;

    private static decimal? Percent(decimal difference, decimal baseline) =>
        baseline == 0m ? null : Math.Round(difference / Math.Abs(baseline) * 100m, 1);
}
