using System.Text.Json;
using Erp.Api.Services.Assistant;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.AI;
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

    private AIFunction Tool(string name) =>
        CreateTools().CreateFunctions(_companyId).OfType<AIFunction>().Single(function => function.Name == name);

    /// <summary>Calls a tool the way the function invocation middleware does, with the model's arguments.</summary>
    private async Task<string> CallAsync(string name, object arguments)
    {
        var values = JsonSerializer.SerializeToElement(arguments)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());

        var result = await Tool(name).InvokeAsync(new AIFunctionArguments(values));

        return ((JsonElement)result!).GetString()!;
    }

    [Fact]
    public async Task Monthly_sales_asks_for_the_callers_company_and_returns_camel_case_json()
    {
        _analytics.GetMonthlySalesAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), Arg.Any<CancellationToken>())
            .Returns([new MonthlySalesDto(2026, 1, 100m, 123m, 2)]);

        var json = await CallAsync(
            AssistantTools.MonthlySales, new { startDate = "2026-01-01", endDate = "2026-02-28" });

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

        await CallAsync(AssistantTools.CompareYears, new { year = 2026 });

        await _analytics.Received(1).CompareYearsAsync(_companyId, 2026, 2025, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Top_customers_caps_how_many_the_model_can_ask_for()
    {
        _analytics.GetTopCustomersAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await CallAsync(
            AssistantTools.TopCustomers, new { startDate = "2026-01-01", endDate = "2026-12-31", count = 100000 });

        await _analytics.Received(1).GetTopCustomersAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 50, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("2026-1-1", "2026-02-28")]
    [InlineData("not a date", "2026-02-28")]
    [InlineData("2026-03-01", "2026-02-28")]
    [InlineData("2000-01-01", "2026-12-31")]
    public async Task Monthly_sales_tells_the_model_about_dates_it_cannot_use(string start, string end)
    {
        var result = await CallAsync(AssistantTools.MonthlySales, new { startDate = start, endDate = end });

        result.Should().StartWith("Error:");
        await _analytics.DidNotReceiveWithAnyArgs()
            .GetMonthlySalesAsync(default, default, default, CancellationToken.None);
    }

    [Fact]
    public void No_tool_takes_the_company_as_a_parameter()
    {
        // The company comes from the request, which has been checked against the caller. A tool
        // that accepted one would let the model name someone else's.
        var tools = CreateTools().CreateFunctions(_companyId).OfType<AIFunction>().ToList();

        tools.Should().HaveCount(3);

        foreach (var tool in tools)
        {
            tool.JsonSchema.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .Should().NotContain(name => name.Contains("company", StringComparison.OrdinalIgnoreCase));
        }
    }
}
