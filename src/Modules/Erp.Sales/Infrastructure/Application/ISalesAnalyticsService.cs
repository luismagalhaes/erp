using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

/// <summary>
/// Answers "how are sales going" from the issued documents. What the assistant reads, but nothing
/// here knows about it: these are figures any report could ask for.
/// </summary>
public interface ISalesAnalyticsService
{
    /// <summary>Net sales per month, oldest first. Months with no sales are present, with zero.</summary>
    Task<IReadOnlyList<MonthlySalesDto>> GetMonthlySalesAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>Net sales of one calendar year against another, month by month.</summary>
    Task<YearComparisonDto> CompareYearsAsync(
        Guid companyId,
        int year,
        int previousYear,
        CancellationToken cancellationToken = default);

    /// <summary>The customers that bought the most in a period, best first.</summary>
    Task<IReadOnlyList<CustomerSalesDto>> GetTopCustomersAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        int count,
        CancellationToken cancellationToken = default);
}
