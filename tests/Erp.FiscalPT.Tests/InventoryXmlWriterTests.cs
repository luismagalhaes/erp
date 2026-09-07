using System.Xml.Linq;
using Erp.FiscalPT.Inventory;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class InventoryXmlWriterTests
{
    private static XNamespace Ns(InventoryFileVersion version) => InventoryConstants.Namespace(version);

    private static InventoryFileHeader Header() => new()
    {
        TaxRegistrationNumber = "500123456",
        FiscalYear = 2026,
        EndDate = new DateOnly(2026, 12, 31)
    };

    private static InventoryFileLine Line(
        string productCode = "ART001",
        decimal quantity = 10m,
        decimal value = 100m) => new()
    {
        ProductCategory = "M",
        ProductCode = productCode,
        ProductDescription = "Artigo de teste",
        ProductNumberCode = "5601234567890",
        ClosingStockQuantity = quantity,
        UnitOfMeasure = "UN",
        ClosingStockValue = value
    };

    private static InventoryFile File(
        InventoryFileVersion version,
        params InventoryFileLine[] lines) => new()
    {
        Version = version,
        Header = Header(),
        Lines = lines
    };

    private static XElement Root(InventoryFile file) => InventoryXmlWriter.Build(file).Root!;

    [Theory]
    [InlineData(InventoryFileVersion.QuantitiesOnly, "urn:StockFile:PT_1_02", "1_02")]
    [InlineData(InventoryFileVersion.Valued, "urn:StockFile:PT_2_01", "2_01")]
    public void Build_uses_the_namespace_and_version_of_the_chosen_schema(
        InventoryFileVersion version,
        string expectedNamespace,
        string expectedFileVersion)
    {
        var root = Root(File(version));

        root.Name.Should().Be(XNamespace.Get(expectedNamespace) + "StockFile");
        root.Element(Ns(version) + "StockHeader")!
            .Element(Ns(version) + "FileVersion")!.Value.Should().Be(expectedFileVersion);
    }

    [Theory]
    [InlineData(InventoryFileVersion.QuantitiesOnly)]
    [InlineData(InventoryFileVersion.Valued)]
    public void Build_writes_the_header_in_schema_order(InventoryFileVersion version)
    {
        var header = Root(File(version)).Element(Ns(version) + "StockHeader")!;

        header.Elements().Select(x => x.Name.LocalName).Should().ContainInOrder(
            "FileVersion", "TaxRegistrationNumber", "FiscalYear", "EndDate", "NoStock");

        header.Element(Ns(version) + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
        header.Element(Ns(version) + "EndDate")!.Value.Should().Be("2026-12-31");
    }

    /// <summary>
    /// NoStock has to say the same thing as the lines that follow, so it is derived rather than
    /// taken from the caller.
    /// </summary>
    [Fact]
    public void Build_says_there_is_no_stock_when_there_are_no_lines()
    {
        Root(File(InventoryFileVersion.Valued))
            .Element(Ns(InventoryFileVersion.Valued) + "StockHeader")!
            .Element(Ns(InventoryFileVersion.Valued) + "NoStock")!.Value.Should().Be("true");
    }

    [Fact]
    public void Build_says_there_is_stock_when_there_are_lines()
    {
        var file = File(InventoryFileVersion.Valued, Line());

        Root(file).Element(Ns(file.Version) + "StockHeader")!
            .Element(Ns(file.Version) + "NoStock")!.Value.Should().Be("false");
    }

    [Fact]
    public void Build_writes_the_line_fields_in_schema_order()
    {
        var file = File(InventoryFileVersion.Valued, Line());

        Root(file).Element(Ns(file.Version) + "Stock")!.Elements()
            .Select(x => x.Name.LocalName).Should().ContainInOrder(
                "ProductCategory",
                "ProductCode",
                "ProductDescription",
                "ProductNumberCode",
                "ClosingStockQuantity",
                "UnitOfMeasure",
                "ClosingStockValue");
    }

    /// <summary>The older schema has no such element, so writing it would fail validation.</summary>
    [Fact]
    public void Build_leaves_the_value_out_of_the_quantities_only_file()
    {
        var file = File(InventoryFileVersion.QuantitiesOnly, Line());

        Root(file).Element(Ns(file.Version) + "Stock")!
            .Element(Ns(file.Version) + "ClosingStockValue").Should().BeNull();
    }

    [Fact]
    public void Build_writes_the_value_with_two_decimals()
    {
        var file = File(InventoryFileVersion.Valued, Line(value: 12.5m));

        Root(file).Element(Ns(file.Version) + "Stock")!
            .Element(Ns(file.Version) + "ClosingStockValue")!.Value.Should().Be("12.50");
    }

    [Fact]
    public void Build_writes_one_stock_element_per_product()
    {
        var file = File(InventoryFileVersion.Valued, Line(), Line("ART002", 5m));

        Root(file).Elements(Ns(file.Version) + "Stock").Should().HaveCount(2);
    }

    /// <summary>A product with no barcode falls back to its code, which the field allows.</summary>
    [Fact]
    public void Build_falls_back_to_the_product_code_when_there_is_no_barcode()
    {
        var file = File(InventoryFileVersion.Valued, new InventoryFileLine
        {
            ProductCode = "ART001",
            ProductDescription = "Artigo de teste",
            ProductNumberCode = string.Empty,
            ClosingStockQuantity = 10m
        });

        Root(file).Element(Ns(file.Version) + "Stock")!
            .Element(Ns(file.Version) + "ProductNumberCode")!.Value.Should().Be("ART001");
    }

    [Fact]
    public void Build_writes_quantities_with_an_invariant_decimal_point()
    {
        var file = File(InventoryFileVersion.Valued, Line(quantity: 12.5m));

        Root(file).Element(Ns(file.Version) + "Stock")!
            .Element(Ns(file.Version) + "ClosingStockQuantity")!.Value.Should().Be("12.5");
    }

    [Fact]
    public void BuildFileName_carries_the_tax_id_and_the_reference_date()
    {
        InventoryXmlWriter.BuildFileName(Header()).Should().Be("Inventario_500123456_20261231.xml");
    }

    [Fact]
    public void Serialize_writes_utf8_without_a_byte_order_mark()
    {
        var bytes = InventoryXmlWriter.Serialize(File(InventoryFileVersion.Valued));

        bytes.Should().StartWith("<?xml"u8.ToArray());
    }

    // --- Against the official schemas ---

    [Theory]
    [InlineData(InventoryFileVersion.QuantitiesOnly)]
    [InlineData(InventoryFileVersion.Valued)]
    public void A_file_with_stock_passes_the_official_schema(InventoryFileVersion version)
    {
        var file = File(version, Line(), Line("ART002", 2.75m, 30.25m));

        InventorySchemaValidator.Validate(InventoryXmlWriter.Build(file), version)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(InventoryFileVersion.QuantitiesOnly)]
    [InlineData(InventoryFileVersion.Valued)]
    public void A_file_without_stock_passes_the_official_schema(InventoryFileVersion version)
    {
        InventorySchemaValidator.Validate(InventoryXmlWriter.Build(File(version)), version)
            .Should().BeEmpty();
    }

    /// <summary>
    /// Validating the bytes and validating the document have to agree — the service hands over the
    /// bytes but validates the document.
    /// </summary>
    [Fact]
    public void Validating_the_serialized_bytes_agrees_with_validating_the_document()
    {
        var file = File(InventoryFileVersion.Valued, Line());

        InventorySchemaValidator.Validate(InventoryXmlWriter.Serialize(file), file.Version)
            .Should().BeEmpty();
    }

    /// <summary>Each schema only knows its own namespace, so the wrong version is caught.</summary>
    [Fact]
    public void Validating_against_the_wrong_version_fails()
    {
        var file = File(InventoryFileVersion.Valued, Line());

        InventorySchemaValidator
            .Validate(InventoryXmlWriter.Build(file), InventoryFileVersion.QuantitiesOnly)
            .Should().NotBeEmpty();
    }

    /// <summary>Biological assets were only added in the valued schema.</summary>
    [Fact]
    public void The_biological_assets_category_belongs_to_the_valued_schema_only()
    {
        InventoryConstants.IsKnownCategory(InventoryFileVersion.Valued, "B").Should().BeTrue();
        InventoryConstants.IsKnownCategory(InventoryFileVersion.QuantitiesOnly, "B").Should().BeFalse();
    }
}
