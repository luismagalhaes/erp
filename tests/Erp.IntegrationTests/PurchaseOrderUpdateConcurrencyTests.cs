using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Diagnostic: reproduces, against the real database, editing a purchase order right after
/// creating it — exactly what the "Guardar" button on the edit page does — to find out why it
/// always throws a concurrency exception.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class PurchaseOrderUpdateConcurrencyTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Editing_an_order_right_after_creating_it_does_not_throw()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var created = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseOrderService>().CreateAsync(
                new CreatePurchaseOrderRequest(
                    scenario.CompanyId,
                    scenario.SupplierId,
                    scenario.Supplier,
                    new DateOnly(2026, 3, 1),
                    scenario.WarehouseId,
                    [new PurchaseOrderLineRequest("ART001", "Artigo de teste", 10m, 5m)]),
                "user-1"));

        var act = () => scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseOrderService>().UpdateAsync(
                created.Id,
                new UpdatePurchaseOrderRequest(
                    new DateOnly(2026, 3, 1),
                    scenario.WarehouseId,
                    [new PurchaseOrderLineRequest("ART002", "Outro artigo", 3m, 20m)],
                    new DateOnly(2026, 3, 15))));

        await act.Should().NotThrowAsync();
    }
}
