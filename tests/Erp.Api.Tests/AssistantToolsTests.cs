using System.Text.Json;
using Erp.Api.Services.Assistant;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Erp.Api.Tests;

/// <summary>
/// What the model is allowed to ask for. The tools are the only door from the assistant to the
/// company's data, so their input handling is what keeps a confused or manipulated model inside it.
/// </summary>
public class AssistantToolsTests
{
    private readonly ISalesAnalyticsService _analytics = Substitute.For<ISalesAnalyticsService>();
    private readonly Guid _companyId = Guid.NewGuid();

    private AssistantTools CreateTools() => new(_analytics);

    private static IReadOnlyDictionary<string, JsonElement> Input(object value) =>
        JsonSerializer.SerializeToElement(value)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());

    [Fact]
    public async Task Monthly_sales_asks_for_the_callers_company_and_returns_camel_case_json()
    {
        _analytics.GetMonthlySalesAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), Arg.Any<CancellationToken>())
            .Returns([new MonthlySalesDto(2026, 1, 100m, 123m, 2)]);

        var json = await CreateTools().ExecuteAsync(
            _companyId,
            AssistantTools.MonthlySales,
            Input(new { start_date = "2026-01-01", end_date = "2026-02-28" }),
            CancellationToken.None);

        using var document = JsonDocument.Parse(json);
        var month = document.RootElement[0];

        month.GetProperty("year").GetInt32().Should().Be(2026);
        month.GetProperty("netTotal").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Comparing_years_defaults_the_previous_year_to_the_one_before()
    {
        _analytics.CompareYearsAsync(_companyId, 2026, 2025, Arg.Any<CancellationToken>())
            .Returns(new YearComparisonDto(2026, 2025, [], 0m, 0m, 0m, null));

        await CreateTools().ExecuteAsync(
            _companyId, AssistantTools.CompareYears, Input(new { year = 2026 }), CancellationToken.None);

        await _analytics.Received(1).CompareYearsAsync(_companyId, 2026, 2025, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Top_customers_caps_how_many_the_model_can_ask_for()
    {
        _analytics.GetTopCustomersAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateTools().ExecuteAsync(
            _companyId,
            AssistantTools.TopCustomers,
            Input(new { start_date = "2026-01-01", end_date = "2026-12-31", count = 100000 }),
            CancellationToken.None);

        await _analytics.Received(1).GetTopCustomersAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 50, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("2026-1-1", "2026-02-28")]
    [InlineData("not a date", "2026-02-28")]
    [InlineData("2026-03-01", "2026-02-28")]
    [InlineData("2000-01-01", "2026-12-31")]
    public async Task Monthly_sales_refuses_dates_it_cannot_use(string start, string end)
    {
        var act = () => CreateTools().ExecuteAsync(
            _companyId,
            AssistantTools.MonthlySales,
            Input(new { start_date = start, end_date = end }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        await _analytics.DidNotReceiveWithAnyArgs()
            .GetMonthlySalesAsync(default, default, default, CancellationToken.None);
    }

    [Fact]
    public async Task An_unknown_tool_is_refused()
    {
        var act = () => CreateTools().ExecuteAsync(_companyId, "delete_everything", Input(new { }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void No_tool_takes_the_company_as_a_parameter()
    {
        // The company comes from the request, which has been checked against the caller. A tool
        // that accepted one would let the model name someone else's.
        foreach (var tool in AssistantTools.Definitions)
        {
            tool.TryPickTool(out var definition).Should().BeTrue();
            definition!.InputSchema.Properties!.Keys
                .Should().NotContain(key => key.Contains("company", StringComparison.OrdinalIgnoreCase));
        }
    }
}
