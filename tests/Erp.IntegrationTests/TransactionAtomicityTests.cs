using Erp.Inventory.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// A document and the stock it moves, written together or not at all.
/// </summary>
/// <remarks>
/// Two modules write inside one transaction: Sales inserts the document, Inventory the ledger entry
/// and the balance. That they share a context is the whole reason the single-DbContext consolidation
/// happened, and a substituted storage cannot show it — it would report both writes as done whether
/// or not they ever reached the same transaction.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class TransactionAtomicityTests(SqlServerFixture fixture)
{
    private static CreateInvoiceRequest Invoice(Guid companyId, Guid seriesId, Guid? warehouseId) =>
        new(companyId,
            seriesId,
            new DateOnly(2026, 3, 15),
            new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
            [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 4m, 100m, "NOR", 23m)],
            WarehouseId: warehouseId);

    [Fact]
    public async Task Issuing_writes_the_document_and_its_stock_movement_together()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var seriesId = await scenario.GivenCommunicatedSeriesAsync(stockEffect: "Out");

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .IssueAsync(Invoice(scenario.CompanyId, seriesId, scenario.WarehouseId), "user-1"));

        var balances = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockService>()
                .GetBalancesAsync(scenario.CompanyId, scenario.WarehouseId, "ART001"));

        balances.Should().ContainSingle().Which.Quantity.Should().Be(-4m, "a sale takes the goods out");
    }

    /// <summary>
    /// The rollback side, and the one that matters: a series that moves stock but no warehouse to
    /// move it in fails <b>after</b> the document is saved and before the transaction commits. The
    /// number must not survive that, or the series would have a hole in it.
    /// </summary>
    [Fact]
    public async Task A_failure_after_the_document_is_saved_leaves_neither_it_nor_its_number()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var seriesId = await scenario.GivenCommunicatedSeriesAsync(stockEffect: "Out");

        var issuing = () => scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .IssueAsync(Invoice(scenario.CompanyId, seriesId, warehouseId: null), "user-1"));

        await issuing.Should().ThrowAsync<ArgumentException>().WithMessage("*warehouse*");

        var documents = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().GetAllAsync(scenario.CompanyId));

        documents.Should().BeEmpty("the transaction rolled back, so the document never existed");

        // And the series is untouched, so the next document is number one.
        var issued = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .IssueAsync(Invoice(scenario.CompanyId, seriesId, scenario.WarehouseId), "user-1"));

        issued.DocumentNumber.Should().EndWith("/1", "a rolled back issue must not burn a number");
    }

    /// <summary>Voiding puts the goods back, by writing the opposite movement rather than deleting.</summary>
    [Fact]
    public async Task Voiding_a_document_returns_the_stock_it_moved()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var seriesId = await scenario.GivenCommunicatedSeriesAsync(stockEffect: "Out");

        var issued = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .IssueAsync(Invoice(scenario.CompanyId, seriesId, scenario.WarehouseId), "user-1"));

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .VoidAsync(issued.Id, "Erro de faturação", "user-2"));

        var balances = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockService>()
                .GetBalancesAsync(scenario.CompanyId, scenario.WarehouseId, "ART001"));

        balances.Should().ContainSingle().Which.Quantity.Should().Be(0m);

        // Both movements stay: the ledger is append-only, so undoing is a second entry.
        var check = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockService>().CheckAsync(scenario.CompanyId, "ART001"));

        check.Lines.Should().ContainSingle().Which.EntryCount.Should().Be(2);
        check.ProductsWithDifference.Should().Be(0);
    }
}
