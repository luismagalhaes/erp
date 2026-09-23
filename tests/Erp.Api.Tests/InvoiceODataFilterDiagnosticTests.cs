using Erp.Api.Services;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Erp.Api.Tests;

[ApiController]
[Route("test/invoices")]
public sealed class TestInvoicesController : ControllerBase
{
    public static readonly List<InvoiceListItemDto> Items =
    [
        new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "FT A2026/1",
            DocumentType = "FT",
            Atcud = "X-1",
            DocumentDate = new DateOnly(2026, 3, 10),
            CustomerName = "Cliente Teste",
            CustomerTaxId = "500999999",
            NetTotal = 100m,
            TaxPayable = 23m,
            GrossTotal = 123m,
            Status = "N"
        },
        new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "NC A2026/1",
            DocumentType = "NC",
            Atcud = "X-2",
            DocumentDate = new DateOnly(2026, 3, 11),
            CustomerName = "Cliente Teste",
            CustomerTaxId = "500999999",
            NetTotal = 10m,
            TaxPayable = 2.3m,
            GrossTotal = 12.3m,
            Status = "N"
        },
        new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "FT A2026/2",
            DocumentType = "FT",
            Atcud = "X-3",
            DocumentDate = new DateOnly(2026, 3, 12),
            CustomerName = "Cliente Teste",
            CustomerTaxId = "500999999",
            NetTotal = 100m,
            TaxPayable = 23m,
            GrossTotal = 123m,
            Status = "A"
        }
    ];

    [HttpGet("odata")]
    public ActionResult<ODataCollection<InvoiceListItemDto>> Query(ODataQueryOptions<InvoiceListItemDto> options) =>
        Ok(ODataQueryExecutor.Execute(Items.AsQueryable(), options));
}

/// <summary>
/// Reproduces, over real HTTP through the real OData query-option parser, the exact $filter string
/// InvoicePickerDialog/InvoiceAutocomplete send for the credit note document picker, so a change to
/// that filter (or to how ODataQuery builds it) that stops matching real documents is caught here
/// instead of showing up as an empty picker.
/// </summary>
public class InvoiceODataFilterDiagnosticTests
{
    private static async Task<HttpClient> StartClientAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(TestInvoicesController).Assembly)
            .AddOData(o => o.Select().Filter().OrderBy().Count().SetMaxTop(500));

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        return app.GetTestClient();
    }

    [Fact]
    public async Task The_exact_filter_the_picker_sends_returns_only_the_customers_rectifiable_documents()
    {
        var client = await StartClientAsync();

        // Exactly what ODataQuery.ToQueryString() builds for CustomerTaxId + Status + DocumentType.
        var filter = "(CustomerTaxId eq '500999999') and (Status ne 'A') and (DocumentType ne 'NC' and DocumentType ne 'ND')";
        var url = $"/test/invoices/odata?$filter={Uri.EscapeDataString(filter)}&$count=true";

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"the request should succeed, got {(int)response.StatusCode} {response.StatusCode}: {body}");
        body.Should().Contain("FT A2026/1");
        body.Should().NotContain("NC A2026/1", "a credit note is not itself rectifiable");
        body.Should().NotContain("FT A2026/2", "it is voided");
    }

    [Fact]
    public async Task Typing_part_of_the_document_number_finds_it_through_the_quick_search_filter()
    {
        var client = await StartClientAsync();

        // Exactly what ODataQuery.ContainsAny("DocumentNumber") builds for InvoiceAutocomplete's own
        // quick search, combined with the same customer/status/type filters the picker uses.
        var filter =
            "(contains(tolower(DocumentNumber), 'a2026/1')) and (CustomerTaxId eq '500999999') " +
            "and (Status ne 'A') and (DocumentType ne 'NC' and DocumentType ne 'ND')";
        var url = $"/test/invoices/odata?$filter={Uri.EscapeDataString(filter)}&$count=true";

        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"the request should succeed, got {(int)response.StatusCode} {response.StatusCode}: {body}");
        body.Should().Contain("FT A2026/1");
    }
}
