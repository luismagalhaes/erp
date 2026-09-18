using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Current account statements, read through the real database.
/// </summary>
/// <remarks>
/// A statement is only as right as the queries under it: the customer's documents are selected by
/// tax id, a voided document is known by a row in another table, and a receipt by its party. A
/// substituted storage hands back whatever the test put in it, so none of that is exercised there.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class AccountStatementTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_customer_statement_reads_that_customers_invoices_and_receipts()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var invoiceSeries = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "None");
        var receiptSeries = await scenario.GivenCommunicatedSeriesAsync("RG");

        Task<InvoiceDetailDto> IssueAsync(string taxId, DateOnly date) =>
            scenario.InScopeAsync(services =>
                services.GetRequiredService<ISalesDocumentService>().IssueAsync(
                    new CreateInvoiceRequest(
                        scenario.CompanyId,
                        invoiceSeries,
                        date,
                        new CustomerRequest(taxId, "Cliente Teste", "Rua do Cliente"),
                        [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, 100m, "NOR", 23m)]),
                    "user-1"));

        var old = await IssueAsync("500999999", new DateOnly(2026, 1, 10));
        var current = await IssueAsync("500999999", new DateOnly(2026, 2, 10));
        var voided = await IssueAsync("500999999", new DateOnly(2026, 2, 11));
        await IssueAsync("500888888", new DateOnly(2026, 2, 12));

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().VoidAsync(voided.Id, "Engano", "user-1"));

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<IPaymentService>().IssueAsync(
                new CreatePaymentRequest(
                    scenario.CompanyId,
                    receiptSeries,
                    new DateOnly(2026, 2, 20),
                    "500999999",
                    "Cliente Teste",
                    [new CreatePaymentLineRequest(current.Id, 23m)],
                    [new CreatePaymentMethodRequest("TB", 23m, new DateOnly(2026, 2, 20))]),
                "user-1"));

        var statement = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ICustomerStatementService>().GetAsync(
                scenario.CompanyId, "500999999", "Cliente Teste", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));

        statement.OpeningBalance.Should().Be(old.GrossTotal);
        statement.Entries.Should().HaveCount(2, "the voided invoice and the other customer stay out");
        statement.Entries[0].SourceId.Should().Be(current.Id);
        statement.Entries[1].Credit.Should().Be(23m);
        statement.ClosingBalance.Should().Be(old.GrossTotal + current.GrossTotal - 23m);
    }

    [Fact]
    public async Task A_supplier_statement_reads_that_suppliers_documents_and_payments()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        Task<PurchaseInvoiceDto> RecordAsync(string type, decimal net) =>
            scenario.InScopeAsync(services =>
                services.GetRequiredService<IPurchaseInvoiceService>().RecordAsync(
                    new RecordPurchaseInvoiceRequest(
                        scenario.CompanyId,
                        scenario.SupplierId,
                        scenario.Supplier,
                        type,
                        $"{type} {Guid.NewGuid().ToString("N")[..6]}",
                        new DateOnly(2026, 3, 12),
                        new DateOnly(2026, 3, 14),
                        [new PurchaseInvoiceLineRequest("SRV", "Transporte", 1m, net, DeductionNature: "OtherGoodsAndServices")]),
                    "user-1"));

        var invoice = await RecordAsync("FT", 100m);
        var creditNote = await RecordAsync("NC", 20m);

        await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierPaymentService>().RecordAsync(
                new CreateSupplierPaymentRequest(
                    scenario.CompanyId,
                    scenario.SupplierId,
                    scenario.Supplier,
                    new DateOnly(2026, 4, 10),
                    [
                        new SupplierPaymentLineRequest("PurchaseInvoice", invoice.Id, 123m),
                        new SupplierPaymentLineRequest("PurchaseInvoice", creditNote.Id, 24.6m)
                    ],
                    [new SupplierPaymentMethodRequest("TB", 98.4m, new DateOnly(2026, 4, 10))]),
                "user-1"));

        var statement = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISupplierStatementService>().GetAsync(
                scenario.CompanyId, scenario.SupplierId, "Fornecedor Teste, Lda", "501234567"));

        statement.Entries.Should().HaveCount(3);
        statement.TotalCredit.Should().Be(123m);
        statement.TotalDebit.Should().Be(24.6m + 98.4m);
        statement.ClosingBalance.Should().Be(0m);
    }
}
