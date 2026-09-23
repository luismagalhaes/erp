using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// Diagnostic for the credit note document picker: reproduces, against the real database, the
/// exact filter the picker's OData query applies (customer, not voided, not itself a rectifying
/// document) to find out whether the query composition is the reason the picker comes back empty.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class InvoiceRectifiablePickerQueryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Filtering_the_invoice_query_by_customer_status_and_document_type_finds_the_document()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();
        var series = await scenario.GivenCommunicatedSeriesAsync("FT", stockEffect: "None");

        var invoice = await scenario.InScopeAsync(services =>
            services.GetRequiredService<ISalesDocumentService>().IssueAsync(
                new CreateInvoiceRequest(
                    scenario.CompanyId,
                    series,
                    new DateOnly(2026, 3, 10),
                    new CustomerRequest("500999999", "Cliente Teste", "Rua do Cliente"),
                    [new CreateInvoiceLineRequest("ART001", "Artigo de teste", 1m, 100m, "NOR", 23m)]),
                "user-1"));

        var found = await scenario.InScopeAsync(services =>
        {
            var query = services.GetRequiredService<ISalesDocumentService>().Query(scenario.CompanyId);

            var result = query
                .Where(x => x.CustomerTaxId == "500999999")
                .Where(x => x.Status != "A")
                .Where(x => x.DocumentType != "NC" && x.DocumentType != "ND")
                .ToList();

            return Task.FromResult(result);
        });

        found.Should().ContainSingle(x => x.Id == invoice.Id);
    }
}
