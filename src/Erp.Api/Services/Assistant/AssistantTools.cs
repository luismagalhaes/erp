using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.Extensions.AI;

namespace Erp.Api.Services.Assistant;

/// <summary>
/// What the assistant can look at. Each tool is a question the ERP can already answer; the model
/// chooses which to ask and reads the figures back, it never computes them.
/// </summary>
/// <remarks>
/// The company is never a parameter of a tool. It comes from the request, which
/// <c>RequireCompanyAccessFilter</c> has already checked against the caller, so a model that was
/// talked into asking about another tenant has no way to name one.
/// </remarks>
public sealed class AssistantTools(ISalesAnalyticsService salesAnalytics)
{
    public const string MonthlySales = "get_monthly_sales";
    public const string CompareYears = "compare_sales_years";
    public const string TopCustomers = "get_top_customers";

    private const int MaxPeriodMonths = 60;
    private const int MaxCustomers = 50;
    private const int DefaultCustomers = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The tools bound to one company. Built per request, so the company never appears in a tool's
    /// schema: the model can only ask questions about the company it is talking in.
    /// </summary>
    public IList<AITool> CreateFunctions(Guid companyId) =>
    [
        AIFunctionFactory.Create(
            ([Description("First day of the period, as YYYY-MM-DD.")] string startDate,
             [Description("Last day of the period, as YYYY-MM-DD.")] string endDate,
             CancellationToken cancellationToken) =>
                RunAsync(() =>
                {
                    var start = Period(startDate, endDate, out var end);

                    return salesAnalytics.GetMonthlySalesAsync(companyId, start, end, cancellationToken);
                }),
            MonthlySales,
            "Net sales (without VAT) and gross sales (with VAT) of the company, month by month, " +
            "between two dates. Credit notes are already deducted and voided documents left out. " +
            "Months with no sales are returned with zero. Use it to compare months or to see a trend."),

        AIFunctionFactory.Create(
            ([Description("The year being analysed, e.g. the current year.")] int year,
             [Description("The year to compare against. Defaults to year - 1.")] int? previousYear = null,
             CancellationToken cancellationToken = default) =>
                RunAsync(() => CompareYearsAsync(companyId, year, previousYear, cancellationToken)),
            CompareYears,
            "Net sales of one calendar year against another, month by month and in total, with the " +
            "difference and the percentage change already computed. Use it for any 'this year " +
            "versus last year' question."),

        AIFunctionFactory.Create(
            ([Description("First day of the period, as YYYY-MM-DD.")] string startDate,
             [Description("Last day of the period, as YYYY-MM-DD.")] string endDate,
             [Description("How many customers to return, up to 50. Defaults to 10.")] int? count = null,
             CancellationToken cancellationToken = default) =>
                RunAsync(() =>
                {
                    var start = Period(startDate, endDate, out var end);

                    return salesAnalytics.GetTopCustomersAsync(
                        companyId,
                        start,
                        end,
                        Math.Clamp(count ?? DefaultCustomers, 1, MaxCustomers),
                        cancellationToken);
                }),
            TopCustomers,
            "The customers that bought the most (net of credit notes, without VAT) between two dates, " +
            "best first. Call it for two different periods to find customers that stopped buying.")
    ];

    /// <summary>
    /// Runs one tool and returns what the model reads, as JSON. Input the tool cannot use comes back
    /// as an error text instead of an exception, so the model can correct itself; any other failure
    /// still throws and is never shown to the model.
    /// </summary>
    private static async Task<string> RunAsync<T>(Func<Task<T>> query)
    {
        try
        {
            return JsonSerializer.Serialize(await query(), JsonOptions);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private Task<YearComparisonDto> CompareYearsAsync(
        Guid companyId,
        int year,
        int? previousYear,
        CancellationToken cancellationToken)
    {
        var previous = previousYear ?? year - 1;

        if (year is < 1970 or > 2200 || previous is < 1970 or > 2200)
            throw new ArgumentException("The years must be between 1970 and 2200.");

        return salesAnalytics.CompareYearsAsync(companyId, year, previous, cancellationToken);
    }

    private static DateOnly Period(string startDate, string endDate, out DateOnly end)
    {
        var start = ParseDate(startDate, "startDate");
        end = ParseDate(endDate, "endDate");

        if (end < start)
            throw new ArgumentException("'endDate' is before 'startDate'.");

        var months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;

        if (months > MaxPeriodMonths)
            throw new ArgumentException($"The period may not be longer than {MaxPeriodMonths} months.");

        return start;
    }

    private static DateOnly ParseDate(string? value, string name) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ArgumentException($"'{name}' is required and must be a date as YYYY-MM-DD.");
}
