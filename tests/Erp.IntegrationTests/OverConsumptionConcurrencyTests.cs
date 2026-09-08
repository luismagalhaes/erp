using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The chain of "the next step may never exceed the one before it", under concurrency.
/// </summary>
/// <remarks>
/// Credit against an invoice, invoice against a delivery note, receive against an order: each reads
/// how much of the previous step is left and then writes. Two requests reading at the same time both
/// see the same room and both take it — unless the row they measured against was locked first.
/// <para>
/// Each locks a different table, so they fail independently and are worth asserting independently.
/// A substituted storage answers every one of them "there is room".
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class OverConsumptionConcurrencyTests(SqlServerFixture fixture)
{
    private const int Concurrent = 6;

    /// <summary>
    /// Runs the same work several times at once and reports how many got through and why the rest
    /// did not — a count on its own turns "nothing worked" into a puzzle instead of a message.
    /// </summary>
    private sealed record Outcome(int Succeeded, IReadOnlyList<string> Refusals)
    {
        public string Explain() =>
            Refusals.Count == 0 ? "nothing was refused" : string.Join(" | ", Refusals.Distinct());
    }

    private static async Task<Outcome> RaceAsync(Func<Task> work)
    {
        var attempts = Enumerable.Range(0, Concurrent).Select(_ => Task.Run<string?>(async () =>
        {
            try
            {
                await work();
                return null;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.GetBaseException().Message}";
            }
        }));

        var results = await Task.WhenAll(attempts);

        return new Outcome(
            results.Count(refusal => refusal is null),
            [.. results.Where(refusal => refusal is not null).Select(refusal => refusal!)]);
    }

    /// <summary>
    /// A document may not be credited beyond its own value. Six credit notes for the full amount,
    /// issued at once: exactly one may pass.
    /// </summary>
    [Fact]
    public async Task An_invoice_cannot_be_credited_twice_over_by_two_requests_at_once()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var invoiceSeries = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "None");
        var creditSeries = await scenario.GivenCommunicatedSeriesAsync("NC", stockEffect: "None");

        var invoice = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().IssueAsync(
                new CreateInvoiceRequest(
                    scenario.CompanyId,
                    invoiceSeries,
                    new DateOnly(2026, 3, 15),
                    new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
                    [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, 100m, "NOR", 23m)]),
                "user-1"));

        var outcome = await RaceAsync(async () =>
        {
            await using var scope = fixture.CreateScope();

            await scope.ServiceProvider.GetRequiredService<ISalesDocumentService>().IssueAsync(
                new CreateInvoiceRequest(
                    scenario.CompanyId,
                    creditSeries,
                    new DateOnly(2026, 3, 16),
                    new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
                    [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, 100m, "NOR", 23m)],
                    RectifiedDocumentId: invoice.Id,
                    RectificationReason: "Devolução"),
                "user-1");
        });

        outcome.Succeeded.Should().Be(1, "crediting an invoice twice over gives back money that was never taken; refusals were {0}", outcome.Explain());
    }

    /// <summary>
    /// A delivery note may not be invoiced for more than it moved. The same shape of rule, with the
    /// lock on the movement rather than on the document.
    /// </summary>
    [Fact]
    public async Task A_delivery_note_cannot_be_invoiced_twice_over_by_two_requests_at_once()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var noteSeries = await scenario.GivenCommunicatedSeriesAsync("GR", stockEffect: "Out");
        var invoiceSeries = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "Out");

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IStockMovementService>().IssueAsync(
                new CreateStockMovementRequest(
                    scenario.CompanyId,
                    noteSeries,
                    new DateOnly(2026, 3, 15),
                    new MovementPartyRequest("500999999", "Cliente Teste"),
                    new MovementLocationRequest("Rua de Carga", WarehouseId: scenario.WarehouseId.ToString()),
                    new MovementLocationRequest("Rua de Descarga"),
                    new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc),
                    [new CreateStockMovementLineRequest("ART001", "Artigo de teste", 4m, 100m, "NOR", 23m)],
                    WarehouseId: scenario.WarehouseId),
                "user-1"));

        // Through the same door the invoicing screen uses: what is still left to invoice, which is
        // where the line id comes from.
        var pending = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>()
                .GetPendingMovementLinesAsync(scenario.CompanyId));

        var lineId = pending.Should().ContainSingle().Subject.LineId;

        var outcome = await RaceAsync(async () =>
        {
            await using var scope = fixture.CreateScope();

            await scope.ServiceProvider.GetRequiredService<ISalesDocumentService>().IssueAsync(
                new CreateInvoiceRequest(
                    scenario.CompanyId,
                    invoiceSeries,
                    new DateOnly(2026, 3, 16),
                    new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
                    [new CreateInvoiceLineRequest(
                        "ART001", "Artigo de teste", 4m, 100m, "NOR", 23m, OriginatingLineId: lineId)],
                    WarehouseId: scenario.WarehouseId),
                "user-1");
        });

        outcome.Succeeded.Should().Be(1, "the goods moved once, so they may be invoiced once; refusals were {0}", outcome.Explain());
    }

    /// <summary>
    /// What the order line records has to match what actually arrived, however many receipts race.
    /// A lost update here leaves the order believing less came in than did, and nothing shows it
    /// until someone reconciles by hand.
    /// </summary>
    /// <remarks>
    /// Not every attempt gets through, and that is by design rather than by accident: the receipt
    /// number (<c>REC2026/7</c>) is ours and carries no fiscal meaning, so it is generated without
    /// a lock and a collision is caught by the unique index — a failed request costs a number, not
    /// an explanation to the tax authority. What is asserted here is the part that must hold either
    /// way: whatever did get through is counted, exactly once.
    /// </remarks>
    [Fact]
    public async Task The_order_line_counts_exactly_the_receipts_that_got_through()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var order = await scenario.GivenPlacedOrderAsync(quantity: 12m);
        var orderLineId = order.Lines[0].Id;

        var outcome = await RaceAsync(async () =>
        {
            await using var scope = fixture.CreateScope();

            await scope.ServiceProvider.GetRequiredService<IGoodsReceiptService>().CreateAsync(
                new CreateGoodsReceiptRequest(
                    scenario.CompanyId,
                    scenario.SupplierId,
                    scenario.Supplier,
                    new DateOnly(2026, 3, 15),
                    scenario.WarehouseId,
                    [new GoodsReceiptLineRequest("ART001", "Artigo de teste", 2m, 5m, OrderLineId: orderLineId)]),
                "user-1");
        });

        outcome.Succeeded.Should().BeGreaterThan(0, "refusals were {0}", outcome.Explain());

        var receipts = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IGoodsReceiptService>().GetAllAsync(scenario.CompanyId));

        receipts.Should().HaveCount(outcome.Succeeded);
        receipts.Select(receipt => receipt.Number).Should().OnlyHaveUniqueItems();

        var reloaded = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseOrderService>().GetByIdAsync(order.Id));

        reloaded!.Lines[0].ReceivedQuantity.Should().Be(
            outcome.Succeeded * 2m, "every receipt that got through has to count, exactly once");
    }
}
