using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// A supplier credit note taken off a payment, through the real database.
/// </summary>
/// <remarks>
/// The sign of a payment line is not stored: it is derived from the document type, and the
/// properties that derive it are left out of the table. A substituted storage cannot say whether
/// that mapping holds, nor whether the credit already used is read back as the model expects.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SupplierPaymentCreditNoteTests(SqlServerFixture fixture)
{
    private static RecordPurchaseInvoiceRequest Document(CompanyScenario scenario, string type, string number) =>
        new(scenario.CompanyId,
            scenario.SupplierId,
            scenario.Supplier,
            type,
            $"{number}-{Guid.NewGuid().ToString("N")[..6]}",
            new DateOnly(2026, 3, 12),
            new DateOnly(2026, 3, 14),
            [new PurchaseInvoiceLineRequest("SRV", "Transporte", 1m, 100m, DeductionNature: "OtherGoodsAndServices")]);

    [Fact]
    public async Task A_credit_note_is_taken_off_the_payment_and_its_credit_is_used_up()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var invoice = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().RecordAsync(Document(scenario, "FT", "FT 2026/1")));

        var creditNote = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().RecordAsync(Document(scenario, "NC", "NC 2026/1")));

        // 123.00 owed, 23.00 of the 123.00 credit used: 100.00 leaves.
        var payment = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierPaymentService>().RecordAsync(
                new CreateSupplierPaymentRequest(
                    scenario.CompanyId,
                    scenario.SupplierId,
                    scenario.Supplier,
                    new DateOnly(2026, 4, 10),
                    [
                        new SupplierPaymentLineRequest("PurchaseInvoice", invoice.Id, 123m),
                        new SupplierPaymentLineRequest("PurchaseInvoice", creditNote.Id, 23m)
                    ],
                    [new SupplierPaymentMethodRequest("TB", 100m, new DateOnly(2026, 4, 10))]),
                "user-1"));

        payment.Total.Should().Be(100m);

        var stored = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierPaymentService>().GetByIdAsync(payment.Id));

        stored!.Lines.Should().ContainSingle(line => line.IsCredit).Which.AppliedAmount.Should().Be(23m);

        var open = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierPaymentService>().GetPayableDocumentsAsync(scenario.CompanyId));

        open.Should().ContainSingle().Which.Should().Match<PayableDocumentDto>(document =>
            document.DocumentId == creditNote.Id && document.IsCredit && document.OutstandingAmount == 100m);

        // The credit note is in use, so it cannot simply be struck out.
        var voidCreditNote = () => scenario.InScopeAsync(services =>
            services.GetRequiredService<IPurchaseInvoiceService>().VoidAsync(creditNote.Id, "Engano"));

        await voidCreditNote.Should().ThrowAsync<InvalidOperationException>();
    }
}
