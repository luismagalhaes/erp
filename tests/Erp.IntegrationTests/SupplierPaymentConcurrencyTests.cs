using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Paying a supplier, from several requests at once.
/// </summary>
/// <remarks>
/// A payment reads how much a document still owes and then writes. Two requests reading at the same
/// time both see the full debt and both pay it — unless something serialises them. Here that is the
/// counter row the payment number comes from, locked for the whole transaction. A substituted
/// storage cannot tell whether that holds; only a real database can.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SupplierPaymentConcurrencyTests(SqlServerFixture fixture)
{
    private const int Concurrent = 6;

    [Fact]
    public async Task An_invoice_cannot_be_paid_twice_by_two_requests_at_once()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var invoice = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().RecordAsync(
                new RecordPurchaseInvoiceRequest(
                    scenario.CompanyId,
                    scenario.SupplierId,
                    scenario.Supplier,
                    "FT",
                    $"FT 2026/{Guid.NewGuid().ToString("N")[..6]}",
                    new DateOnly(2026, 3, 12),
                    new DateOnly(2026, 3, 14),
                    [new PurchaseInvoiceLineRequest(
                        "SRV", "Transporte", 1m, 100m, DeductionNature: "OtherGoodsAndServices")]),
                "user-1"));

        var attempts = Enumerable.Range(0, Concurrent).Select(_ => Task.Run<string?>(async () =>
        {
            try
            {
                await using var scope = fixture.CreateScope();

                await scope.ServiceProvider.GetRequiredService<ISupplierPaymentService>().RecordAsync(
                    new CreateSupplierPaymentRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        new DateOnly(2026, 4, 10),
                        [new SupplierPaymentLineRequest("PurchaseInvoice", invoice.Id, invoice.GrossTotal)],
                        [new SupplierPaymentMethodRequest("TB", invoice.GrossTotal, new DateOnly(2026, 4, 10))]),
                    "user-1");

                return null;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.GetBaseException().Message}";
            }
        }));

        var results = await Task.WhenAll(attempts);
        var refusals = results.Where(result => result is not null).Distinct();

        results.Count(result => result is null).Should().Be(
            1, "paying a document twice sends money that was never owed; refusals were {0}", string.Join(" | ", refusals));

        var payable = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierPaymentService>().GetPayableDocumentsAsync(scenario.CompanyId));

        payable.Should().BeEmpty();
    }
}
