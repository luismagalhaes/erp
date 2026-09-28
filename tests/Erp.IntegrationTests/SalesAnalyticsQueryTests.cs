using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The sales figures the assistant reads, run against the real database: the query filters on a
/// status that is derived from the append-only status changes, and only SQL Server can say whether
/// that translates.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SalesAnalyticsQueryTests(SqlServerFixture fixture)
{
    private static Task<InvoiceDetailDto> IssueAsync(
        CompanyScenario scenario, Guid seriesId, DateOnly date, decimal unitPrice) =>
        scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().IssueAsync(
                new CreateInvoiceRequest(
                    scenario.CompanyId,
                    seriesId,
                    date,
                    new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
                    [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, unitPrice, "NOR", 23m)]),
                "user-1"));

    [Fact]
    public async Task Monthly_sales_count_issued_documents_and_leave_out_the_voided_ones()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var series = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "None");

        await IssueAsync(scenario, series, new DateOnly(2026, 2, 10), 200m);
        await IssueAsync(scenario, series, new DateOnly(2026, 3, 10), 100m);
        var voided = await IssueAsync(scenario, series, new DateOnly(2026, 3, 20), 50m);

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().VoidAsync(voided.Id, "Erro de emissão", "user-1"));

        var months = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesAnalyticsService>().GetMonthlySalesAsync(
                scenario.CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));

        months.Select(x => (x.Month, x.NetTotal, x.DocumentCount))
            .Should().Equal((1, 0m, 0), (2, 200m, 1), (3, 100m, 1));
    }

    [Fact]
    public async Task Top_customers_reads_the_same_figures()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var series = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "None");

        await IssueAsync(scenario, series, new DateOnly(2026, 4, 10), 80m);
        await IssueAsync(scenario, series, new DateOnly(2026, 5, 10), 20m);

        var top = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesAnalyticsService>().GetTopCustomersAsync(
                scenario.CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), count: 5));

        top.Should().ContainSingle();
        top[0].CustomerName.Should().Be("Cliente Teste");
        top[0].NetTotal.Should().Be(100m);
        top[0].DocumentCount.Should().Be(2);
    }
}
