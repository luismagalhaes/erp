using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.Inventory;
using Erp.Inventory.Application.Services;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Inventory.Tests;

public class InventoryFileServiceTests
{
    private static XNamespace Ns(bool valued) => InventoryConstants.Namespace(
        valued ? InventoryFileVersion.Valued : InventoryFileVersion.QuantitiesOnly);

    private readonly IStockStorage _storage = Substitute.For<IStockStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly List<LedgerMovement> _ledger = [];

    public InventoryFileServiceTests()
    {
        _storage.GetMovementsAsync(
                Arg.Any<Guid>(), Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<LedgerMovement>)[.. _ledger]);
    }

    /// <summary>
    /// One movement into the ledger, which is the only way stock and its cost exist at all. A null
    /// cost is stock the ledger never observed a price for.
    /// </summary>
    private void GivenStock(
        string productCode = "ART001",
        decimal quantity = 10m,
        decimal? unitCost = 25m,
        string description = "Artigo de teste") =>
        _ledger.Add(new LedgerMovement(_warehouseId, productCode, description, quantity, unitCost));

    private InventoryFileService CreateService() => new(_storage);

    private InventoryFileRequest Request(params InventoryFileProductDto[] products) =>
        new(_companyId,
            2026,
            new DateOnly(2026, 12, 31),
            new InventoryCompanyInfo("Empresa Teste", "500123456", "Empresa Teste, Lda"),
            products);

    private static InventoryFileProductDto Product(
        string productCode = "ART001",
        decimal unitCost = 25m,
        string category = "M") =>
        new(productCode, "Artigo de teste", "UN", unitCost, "5601234567890", category);

    private static XElement Parse(InventoryFileResultDto result) =>
        XDocument.Parse(Encoding.UTF8.GetString(result.Content)).Root!;

    [Fact]
    public async Task BuildAsync_reports_the_stock_held_at_the_reference_date()
    {
        GivenStock("ART001", 10m);

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
        GivenStock("ART001", 1m);

        await CreateService().BuildAsync(Request(Product()));

        await _storage.Received(1).GetMovementsAsync(
            _companyId, new DateOnly(2026, 12, 31), null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The whole point of the costing: the file is valued at what the goods actually cost, not at
    /// the standard cost typed into the product file. Here the two disagree, and the ledger wins.
    /// </summary>
    [Fact]
    public async Task BuildAsync_values_the_stock_at_the_cost_the_ledger_observed()
    {
        GivenStock("ART001", 3m, unitCost: 12.5m);

        var result = await CreateService().BuildAsync(Request(Product(unitCost: 99m)));

        Parse(result).Element(Ns(true) + "Stock")!
            .Element(Ns(true) + "ClosingStockValue")!.Value.Should().Be("37.50");
        result.ProductsCostedFromFile.Should().Be(0);
    }

    /// <summary>
    /// Weighted average, not the last price paid: ten at 10 and ten at 20 leave twenty at 15.
    /// </summary>
    [Fact]
    public async Task BuildAsync_values_the_stock_at_the_weighted_average_of_what_was_bought()
    {
        GivenStock("ART001", 10m, unitCost: 10m);
        GivenStock("ART001", 10m, unitCost: 20m);

        var result = await CreateService().BuildAsync(Request(Product()));

        result.TotalQuantity.Should().Be(20m);
        result.TotalValue.Should().Be(300m);
    }

    /// <summary>
    /// Stock the ledger never put a price on — opening stock, or anything from before costing —
    /// falls back to the product file rather than being reported as worthless, and the caller is
    /// told how often that happened.
    /// </summary>
    [Fact]
    public async Task BuildAsync_falls_back_to_the_product_file_when_the_ledger_costed_nothing()
    {
        GivenStock("ART001", 4m, unitCost: null);

        var result = await CreateService().BuildAsync(Request(Product(unitCost: 7m)));

        result.TotalValue.Should().Be(28m);
        result.ProductsCostedFromFile.Should().Be(1);
        result.ProductsWithoutCost.Should().Be(0);
    }

    /// <summary>
    /// The valued communication requires the valuation, so products with no cost have to be visible
    /// rather than quietly reported as worth nothing.
    /// </summary>
    [Fact]
    public async Task BuildAsync_counts_the_products_with_no_cost()
    {
        GivenStock("ART001", 10m);
        GivenStock("ART002", 5m, unitCost: null);

        var result = await CreateService().BuildAsync(
            Request(Product("ART001", 25m), Product("ART002", 0m)));

        result.ProductsWithoutCost.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_leaves_out_products_with_no_stock()
    {
        GivenStock("ART001", 0m);
        GivenStock("ART002", 4m);

        var result = await CreateService().BuildAsync(Request(Product("ART001"), Product("ART002")));

        result.LineCount.Should().Be(1);
    }

    /// <summary>
    /// Stock that went out without coming in is still stock the file has to declare, so it is
    /// reported — but the schema refuses a negative quantity, and the caller is told both things.
    /// </summary>
    [Fact]
    public async Task BuildAsync_reports_a_negative_quantity_rather_than_hiding_it()
    {
        GivenStock("ART001", -2m);

        var result = await CreateService().BuildAsync(Request(Product()));

        result.LineCount.Should().Be(1);
        result.TotalQuantity.Should().Be(-2m);
        result.ProductsWithNegativeStock.Should().Be(1);
        result.ValidationErrors.Should().NotBeEmpty();
    }

    /// <summary>
    /// A product removed from the file still has stock to report, and the ledger kept its name.
    /// </summary>
    [Fact]
    public async Task BuildAsync_falls_back_to_the_description_kept_by_the_ledger()
    {
        GivenStock("ART999", 7m, description: "Artigo descontinuado");

        var result = await CreateService().BuildAsync(Request(Product()));

        Parse(result).Element(Ns(true) + "Stock")!
            .Element(Ns(true) + "ProductDescription")!.Value.Should().Be("Artigo descontinuado");
    }

    [Fact]
    public async Task BuildAsync_names_the_file_after_the_entity_and_the_date()
    {
        GivenStock("ART001", 1m);

        var result = await CreateService().BuildAsync(Request(Product()));

        result.FileName.Should().Be("Inventario_500123456_20261231.xml");
    }

    [Fact]
    public async Task BuildAsync_strips_non_digits_from_the_tax_registration_number()
    {
        GivenStock("ART001", 1m);

        var request = Request(Product()) with
        {
            Company = new InventoryCompanyInfo("Empresa Teste", "PT 500 123 456")
        };

        var result = await CreateService().BuildAsync(request);

        Parse(result).Element(Ns(true) + "StockHeader")!
            .Element(Ns(true) + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
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
        result.ValidationErrors.Should().BeEmpty();
    }

    // --- The two file versions ---

    [Theory]
    [InlineData(true, "2_01")]
    [InlineData(false, "1_02")]
    public async Task BuildAsync_writes_the_version_the_caller_asked_for(bool valued, string fileVersion)
    {
        GivenStock("ART001", 10m);

        var result = await CreateService().BuildAsync(Request(Product()) with { Valued = valued });

        var root = Parse(result);
        root.Name.Namespace.Should().Be(Ns(valued));
        root.Element(Ns(valued) + "StockHeader")!
            .Element(Ns(valued) + "FileVersion")!.Value.Should().Be(fileVersion);
        result.ValidationErrors.Should().BeEmpty();
    }

    /// <summary>The older schema has no ClosingStockValue and would reject one.</summary>
    [Fact]
    public async Task BuildAsync_leaves_the_value_out_of_the_quantities_only_file()
    {
        GivenStock("ART001", 10m);

        var result = await CreateService().BuildAsync(Request(Product()) with { Valued = false });

        Parse(result).Element(Ns(false) + "Stock")!
            .Element(Ns(false) + "ClosingStockValue").Should().BeNull();
    }

    /// <summary>
    /// The value is still computed for the quantities-only file, so the figure can be shown even
    /// though it is not communicated.
    /// </summary>
    [Fact]
    public async Task BuildAsync_still_totals_the_value_on_the_quantities_only_file()
    {
        GivenStock("ART001", 4m, unitCost: 10m);

        var result = await CreateService().BuildAsync(
            Request(Product(unitCost: 10m)) with { Valued = false });

        result.TotalValue.Should().Be(40m);
    }

    /// <summary>
    /// Nothing has to carry a value on the quantities-only file, so a missing cost is not a problem
    /// worth reporting there.
    /// </summary>
    [Fact]
    public async Task BuildAsync_does_not_count_missing_costs_on_the_quantities_only_file()
    {
        // Neither source knows what it cost, so on the valued file this would be counted.
        GivenStock("ART001", 10m, unitCost: null);

        var result = await CreateService().BuildAsync(
            Request(Product(unitCost: 0m)) with { Valued = false });

        result.ProductsWithoutCost.Should().Be(0);
    }

    /// <summary>
    /// Biological assets only exist in the valued schema, so on the older one the category falls
    /// back to merchandise rather than producing a file the tax authority would reject.
    /// </summary>
    [Fact]
    public async Task BuildAsync_falls_back_to_merchandise_for_a_category_the_schema_does_not_know()
    {
        GivenStock("ART001", 10m);

        var valued = await CreateService().BuildAsync(Request(Product(category: "B")));
        var quantitiesOnly = await CreateService().BuildAsync(
            Request(Product(category: "B")) with { Valued = false });

        Parse(valued).Element(Ns(true) + "Stock")!
            .Element(Ns(true) + "ProductCategory")!.Value.Should().Be("B");
        Parse(quantitiesOnly).Element(Ns(false) + "Stock")!
            .Element(Ns(false) + "ProductCategory")!.Value.Should().Be("M");
    }
}
