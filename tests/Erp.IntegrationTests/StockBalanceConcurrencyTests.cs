using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Moving the same product from several requests at once.
/// </summary>
/// <remarks>
/// The balance is a projection kept beside the ledger, and the two are only allowed to disagree
/// never. Keeping them together under concurrency is the job of the <c>UPDLOCK</c> on the balance
/// row: without it two movements read the same figure, each adds its own, and the second silently
/// overwrites the first — the balance ends up short and the ledger is right, which is exactly the
/// drift the stock check exists to catch.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class StockBalanceConcurrencyTests(SqlServerFixture fixture)
{
    private const int Concurrent = 16;

    [Fact]
    public async Task Adjusting_the_same_product_at_the_same_time_loses_nothing()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var adjusting = Enumerable.Range(0, Concurrent).Select(index => Task.Run(async () =>
        {
            await using var scope = fixture.CreateScope();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();

            return await stock.AdjustAsync(
                new AdjustStockRequest(
                    scenario.CompanyId,
                    scenario.WarehouseId,
                    "ART001",
                    "Artigo de teste",
                    Difference: 1m,
                    new DateOnly(2026, 3, 15),
                    $"Acerto {index}"),
                "user-1");
        }));

        await Task.WhenAll(adjusting);

        var balances = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockService>()
                .GetBalancesAsync(scenario.CompanyId, scenario.WarehouseId, "ART001"));

        balances.Should().ContainSingle().Which.Quantity.Should().Be(
            Concurrent, "every movement has to land, not just the last one to write");
    }

    /// <summary>
    /// The same question asked the way the system asks it: the check compares the stored balance
    /// against the ledger replayed. Under concurrency this is what would come apart first.
    /// </summary>
    [Fact]
    public async Task The_balance_still_agrees_with_the_ledger_after_concurrent_movements()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var adjusting = Enumerable.Range(0, Concurrent).Select(index => Task.Run(async () =>
        {
            await using var scope = fixture.CreateScope();
            var stock = scope.ServiceProvider.GetRequiredService<IStockService>();

            // Mixed directions, so a lost update shows up as a wrong figure rather than a smaller one.
            var difference = index % 3 == 0 ? -1m : 2m;

            return await stock.AdjustAsync(
                new AdjustStockRequest(
                    scenario.CompanyId,
                    scenario.WarehouseId,
                    "ART001",
                    "Artigo de teste",
                    difference,
                    new DateOnly(2026, 3, 15),
                    $"Acerto {index}"),
                "user-1");
        }));

        await Task.WhenAll(adjusting);

        var check = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockService>().CheckAsync(scenario.CompanyId, "ART001"));

        check.ProductsWithDifference.Should().Be(0);
        check.ProductsWithValueDifference.Should().Be(0);

        var line = check.Lines.Should().ContainSingle().Subject;
        line.RecordedQuantity.Should().Be(line.LedgerQuantity);
        line.EntryCount.Should().Be(Concurrent, "one ledger entry per movement, none lost");
    }

    /// <summary>
    /// A document line moves stock once. The rule lives in the recorder, but the filtered unique
    /// index on SourceLineId is what makes it true when two requests race — and an index is
    /// something only a database has.
    /// </summary>
    [Fact]
    public async Task The_database_refuses_a_second_ledger_entry_for_the_same_document_line()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var lineId = Guid.NewGuid();

        async Task WriteAsync()
        {
            await using var scope = fixture.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            var entry = StockLedgerEntry.FromDocument(
                scenario.CompanyId,
                scenario.WarehouseId,
                "ART001",
                "Artigo de teste",
                StockDirection.In,
                1m,
                new DateOnly(2026, 3, 15),
                "GR",
                "GR A2026/1",
                Guid.NewGuid(),
                lineId);

            context.Set<StockLedgerEntry>().Add(entry);
            await context.SaveChangesAsync();
        }

        await WriteAsync();

        var second = () => WriteAsync();

        await second.Should().ThrowAsync<DbUpdateException>(
            "the same document line must not be able to move its stock twice");
    }
}
