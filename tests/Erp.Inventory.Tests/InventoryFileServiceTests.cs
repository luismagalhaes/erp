using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.Inventory;
using Erp.Inventory.Application.Configuration;
using Erp.Inventory.Application.Services;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Inventory.Tests;

public class InventoryFileServiceTests
{
    private static readonly XNamespace Ns = InventoryConstants.Namespace;

    private readonly IStockStorage _storage = Substitute.For<IStockStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Dictionary<string, (decimal Quantity, string Description)> _stock = [];

    public InventoryFileServiceTests()
    {
        _storage.SumLedgerAsAtAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyDictionary<string, (decimal, string)>)_stock);
    }

    private InventoryFileService CreateService() =>
        new(_storage, Options.Create(new InventoryFiscalOptions
        {
            IssuerTaxId = "123456789",
            CertificateNumber = "9999"
        }));

    private InventoryFileRequest Request(params InventoryFileProductDto[] products) =>
        new(_companyId,
            2026,
            new DateOnly(2026, 12, 31),
            new InventoryCompanyInfo("Empresa Teste", "500123456", "Empresa Teste, Lda"),
            products);

    private static InventoryFileProductDto Product(
        string productCode = "ART001",
        decimal unitCost = 25m) =>
        new(productCode, "Artigo de teste", "UN", unitCost, "5601234567890");

    private static XElement Parse(InventoryFileResultDto result) =>
        XDocument.Parse(Encoding.UTF8.GetString(result.Content)).Root!;

    [Fact]
    public async Task BuildAsync_reports_the_stock_held_at_the_reference_date()
    {
        _stock["ART001"] = (10m, "Artigo de teste");

        var result = await CreateService().BuildAsync(Request(Product()));

        result.LineCount.Should().Be(1);
        result.TotalQuantity.Should().Be(10m);
        result.TotalValue.Should().Be(250m);
    }

    /// <summary>
    /// The file reports the stock on the last day of the period, which is not the stock today.
    /// </summary>
    [Fact]
    public async Task BuildAsync_asks_the_ledger_for_the_reference_date()
    {
        _stock["ART001"] = (1m, "Artigo");

        await CreateService().BuildAsync(Request(Product()));

        await _storage.Received(1).SumLedgerAsAtAsync(
            _companyId, new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildAsync_values_the_stock_at_the_product_cost()
    {
        _stock["ART001"] = (3m, "Artigo");

        var result = await CreateService().BuildAsync(Request(Product(unitCost: 12.5m)));

        Parse(result).Element(Ns + "Inventory")!
            .Element(Ns + "Line")!
            .Element(Ns + "ClosingStockValue")!.Value.Should().Be("37.50");
    }

    /// <summary>
    /// The communication requires the valuation, so products with no cost have to be visible
    /// rather than quietly reported as worth nothing.
    /// </summary>
    [Fact]
    public async Task BuildAsync_counts_the_products_with_no_cost()
    {
        _stock["ART001"] = (10m, "Artigo um");
        _stock["ART002"] = (5m, "Artigo dois");

        var result = await CreateService().BuildAsync(
            Request(Product("ART001", 25m), Product("ART002", 0m)));

        result.ProductsWithoutCost.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_leaves_out_products_with_no_stock()
    {
        _stock["ART001"] = (0m, "Artigo sem stock");
        _stock["ART002"] = (4m, "Artigo com stock");

        var result = await CreateService().BuildAsync(Request(Product("ART001"), Product("ART002")));

        result.LineCount.Should().Be(1);
    }

    /// <summary>Stock that went out without coming in is still stock the file has to declare.</summary>
    [Fact]
    public async Task BuildAsync_reports_a_negative_quantity_rather_than_hiding_it()
    {
        _stock["ART001"] = (-2m, "Artigo");

        var result = await CreateService().BuildAsync(Request(Product()));

        result.LineCount.Should().Be(1);
        result.TotalQuantity.Should().Be(-2m);
    }

    /// <summary>
    /// A product removed from the file still has stock to report, and the ledger kept its name.
    /// </summary>
    [Fact]
    public async Task BuildAsync_falls_back_to_the_description_kept_by_the_ledger()
    {
        _stock["ART999"] = (7m, "Artigo descontinuado");

        var result = await CreateService().BuildAsync(Request(Product()));

        Parse(result).Element(Ns + "Inventory")!
            .Element(Ns + "Line")!
            .Element(Ns + "ProductDescription")!.Value.Should().Be("Artigo descontinuado");
    }

    [Fact]
    public async Task BuildAsync_names_the_file_after_the_entity_and_the_date()
    {
        _stock["ART001"] = (1m, "Artigo");

        var result = await CreateService().BuildAsync(Request(Product()));

        result.FileName.Should().Be("Inventario_500123456_20261231.xml");
    }

    [Fact]
    public async Task BuildAsync_strips_non_digits_from_the_tax_registration_number()
    {
        _stock["ART001"] = (1m, "Artigo");

        var request = Request(Product()) with
        {
            Company = new InventoryCompanyInfo("Empresa Teste", "PT 500 123 456")
        };

        var result = await CreateService().BuildAsync(request);

        Parse(result).Element(Ns + "Header")!
            .Element(Ns + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
    }

    [Fact]
    public async Task BuildAsync_rejects_a_company_without_a_tax_id()
    {
        var request = Request(Product()) with { Company = new InventoryCompanyInfo("Empresa", "   ") };

        var act = () => CreateService().BuildAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*tax id*");
    }

    /// <summary>The communication is due even when the entity held nothing.</summary>
    [Fact]
    public async Task BuildAsync_produces_a_file_for_an_empty_inventory()
    {
        var result = await CreateService().BuildAsync(Request(Product()));

        result.LineCount.Should().Be(0);
        result.Content.Should().NotBeEmpty();
    }
}
