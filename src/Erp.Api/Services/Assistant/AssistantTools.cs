using System.Globalization;
using System.Text.Json;
using Anthropic.Models.Messages;
using Erp.Sales.Infrastructure.Application;

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

    public static IReadOnlyList<ToolUnion> Definitions { get; } =
    [
        new Tool
        {
            Name = MonthlySales,
            Description =
                "Net sales (without VAT) and gross sales (with VAT) of the company, month by month, " +
                "between two dates. Credit notes are already deducted and voided documents left out. " +
                "Months with no sales are returned with zero. Use it to compare months or to see a trend.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["start_date"] = Schema("string", "First day of the period, as YYYY-MM-DD."),
                    ["end_date"] = Schema("string", "Last day of the period, as YYYY-MM-DD.")
                },
                Required = ["start_date", "end_date"]
            }
        },
        new Tool
        {
            Name = CompareYears,
            Description =
                "Net sales of one calendar year against another, month by month and in total, with the " +
                "difference and the percentage change already computed. Use it for any 'this year " +
                "versus last year' question.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["year"] = Schema("integer", "The year being analysed, e.g. the current year."),
                    ["previous_year"] = Schema("integer", "The year to compare against. Defaults to year - 1.")
                },
                Required = ["year"]
            }
        },
        new Tool
        {
            Name = TopCustomers,
            Description =
                "The customers that bought the most (net of credit notes, without VAT) between two dates, " +
                "best first. Call it for two different periods to find customers that stopped buying.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["start_date"] = Schema("string", "First day of the period, as YYYY-MM-DD."),
                    ["end_date"] = Schema("string", "Last day of the period, as YYYY-MM-DD."),
                    ["count"] = Schema("integer", $"How many customers to return, up to {MaxCustomers}. Defaults to {DefaultCustomers}.")
                },
                Required = ["start_date", "end_date"]
            }
        }
    ];

    /// <summary>
    /// Runs a tool and returns what to hand back to the model, as JSON. Throws
    /// <see cref="ArgumentException"/> for input the tool cannot use, which the caller reports to
    /// the model so it can correct itself.
    /// </summary>
    public async Task<string> ExecuteAsync(
        Guid companyId,
        string name,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        object result = name switch
        {
            MonthlySales => await salesAnalytics.GetMonthlySalesAsync(
                companyId, Period(input, out var end), end, cancellationToken),

            CompareYears => await CompareYearsAsync(companyId, input, cancellationToken),

            TopCustomers => await salesAnalytics.GetTopCustomersAsync(
                companyId,
                Period(input, out var customersEnd),
                customersEnd,
                Math.Clamp(OptionalInt(input, "count") ?? DefaultCustomers, 1, MaxCustomers),
                cancellationToken),

            _ => throw new ArgumentException($"Unknown tool '{name}'.")
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private Task<Erp.Sales.Infrastructure.Contracts.YearComparisonDto> CompareYearsAsync(
        Guid companyId,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        var year = OptionalInt(input, "year") ?? throw new ArgumentException("'year' is required.");
        var previousYear = OptionalInt(input, "previous_year") ?? year - 1;

        if (year is < 1970 or > 2200 || previousYear is < 1970 or > 2200)
            throw new ArgumentException("The years must be between 1970 and 2200.");

        return salesAnalytics.CompareYearsAsync(companyId, year, previousYear, cancellationToken);
    }

    private static DateOnly Period(IReadOnlyDictionary<string, JsonElement> input, out DateOnly end)
    {
        var start = RequiredDate(input, "start_date");
        end = RequiredDate(input, "end_date");

        if (end < start)
            throw new ArgumentException("'end_date' is before 'start_date'.");

        var months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;

        if (months > MaxPeriodMonths)
            throw new ArgumentException($"The period may not be longer than {MaxPeriodMonths} months.");

        return start;
    }

    private static DateOnly RequiredDate(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (input.TryGetValue(key, out var value)
            && value.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new ArgumentException($"'{key}' is required and must be a date as YYYY-MM-DD.");
    }

    private static int? OptionalInt(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        throw new ArgumentException($"'{key}' must be an integer.");
    }

    private static JsonElement Schema(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });
}
