using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.Storage;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Wiping a company, against the real schema: the purger works out its order from the model, so
/// the only honest test is a company full of everything, in a database with every foreign key.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class CompanyDataPurgeTests(SqlServerFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public async Task Wiping_a_company_removes_what_it_did_keeps_what_it_is_and_leaves_others_alone()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var other = await new CompanyScenario(fixture).CreateAsync();

        await FillAsync(scenario);
        await FillAsync(other);

        var deleted = await scenario.InScopeAsync(services =>
            services.GetRequiredService<CompanyDataPurger>().PurgeAsync(scenario.CompanyId));

        deleted.Should().ContainKeys("SalesDocument", "PurchaseInvoice", "PurchaseOrder", "Series", "Product");

        (await CountAsync(scenario)).Should().Be((0, 0, 0, 0));

        // The company is still there, with what makes it one.
        await scenario.InScopeAsync(async services =>
        {
            (await services.GetRequiredService<ICompanyAdminService>().GetByIdAsync(scenario.CompanyId)).Should().NotBeNull();
            (await services.GetRequiredService<IWarehouseService>().GetAllAsync(scenario.CompanyId)).Should().NotBeEmpty();
        });

        // Another company was not touched.
        (await CountAsync(other)).Should().NotBe((0, 0, 0, 0));

        // And the wiped one can be filled again.
        var again = await scenario.InScopeAsync(services =>
            services.GetRequiredService<DemoHistoryService>().ApplyAsync(scenario.CompanyId, "user-1", Today));

        again.Applied.Should().BeTrue();
    }

    private async Task FillAsync(CompanyScenario scenario)
    {
        await scenario.InScopeAsync(services =>
            services.GetRequiredService<DemoDataService>().ApplyAsync(scenario.CompanyId, "user-1"));

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<DemoHistoryService>().ApplyAsync(scenario.CompanyId, "user-1", Today));

        await scenario.GivenPlacedOrderAsync();
    }

    private static Task<(int Invoices, int Purchases, int Orders, int Series)> CountAsync(CompanyScenario scenario) =>
        scenario.InScopeAsync(async services =>
            ((await services.GetRequiredService<ISalesDocumentService>().GetAllAsync(scenario.CompanyId)).Count,
             (await services.GetRequiredService<IPurchaseInvoiceService>().GetAllAsync(scenario.CompanyId)).Count,
             (await services.GetRequiredService<IPurchaseOrderService>().GetAllAsync(scenario.CompanyId)).Count,
             (await services.GetRequiredService<ISeriesService>().GetAllAsync(scenario.CompanyId)).Count));
}
