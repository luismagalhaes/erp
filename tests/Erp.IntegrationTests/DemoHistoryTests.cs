using Erp.Api.Services;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The demo history, issued for real against the database: every invoice, credit note and purchase
/// goes through the ordinary services, so any rule they enforce — numbering, signing, crediting,
/// duplicate supplier documents — would fail this test rather than leave a half made history.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class DemoHistoryTests(SqlServerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public async Task Issues_two_years_of_sales_and_purchases_and_does_nothing_the_second_time()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var first = await scenario.InScopeAsync(services =>
            services.GetRequiredService<DemoHistoryService>().ApplyAsync(scenario.CompanyId, "user-1", Today));

        first.Applied.Should().BeTrue();
        first.Invoices.Should().BeGreaterThan(200);
        first.CreditNotes.Should().BeGreaterThan(0);
        first.VoidedInvoices.Should().BeGreaterThan(0);
        first.PurchaseInvoices.Should().BeGreaterThan(80);

        var second = await scenario.InScopeAsync(services =>
            services.GetRequiredService<DemoHistoryService>().ApplyAsync(scenario.CompanyId, "user-1", Today));

        second.Applied.Should().BeFalse();

        var comparison = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesAnalyticsService>().CompareYearsAsync(scenario.CompanyId, 2026, 2025));

        // The story the data is built to tell: growth overall, and one weak month this year.
        comparison.Months[2].ChangePercent.Should().BeLessThan(-20m, "March of this year is the deliberately weak month");
        comparison.Months[0].CurrentNetTotal.Should().BeGreaterThan(0m);
        comparison.Months[9].CurrentNetTotal.Should().Be(0m, "October has not happened yet");

        var purchases = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().GetAllAsync(scenario.CompanyId));

        purchases.Should().HaveCount(first.PurchaseInvoices);
    }
}
