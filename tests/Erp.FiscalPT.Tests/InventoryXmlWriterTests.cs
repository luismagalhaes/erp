using System.Xml.Linq;
using Erp.FiscalPT.Inventory;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class InventoryXmlWriterTests
{
    private static readonly XNamespace Ns = InventoryConstants.Namespace;

    private static InventoryFileHeader Header() => new()
    {
        TaxRegistrationNumber = "500123456",
        FiscalYear = 2026,
        EndDate = new DateOnly(2026, 12, 31)
    };

    private static InventoryFileLine Line(string productCode = "ART001", decimal quantity = 10m) => new()
    {
        ProductCategory = "M",
        ProductCode = productCode,
        ProductDescription = "Artigo de teste",
        ProductNumberCode = "5601234567890",
        ClosingStockQuantity = quantity,
        UnitOfMeasure = "UN"
    };

    private static XElement Root(InventoryFile file) => InventoryXmlWriter.Build(file).Root!;

    [Fact]
    public void Build_uses_the_official_namespace_and_root()
    {
        var root = Root(new InventoryFile { Header = Header() });

        root.Name.Should().Be(Ns + "StockFile");
        root.Element(Ns + "StockHeader")!.Element(Ns + "FileVersion")!.Value.Should().Be("1_02");
    }

    [Fact]
    public void Build_writes_the_header_in_schema_order()
    {
        var header = Root(new InventoryFile { Header = Header() }).Element(Ns + "StockHeader")!;

        header.Elements().Select(x => x.Name.LocalName).Should().ContainInOrder(
            "FileVersion", "TaxRegistrationNumber", "FiscalYear", "EndDate", "NoStock");

        header.Element(Ns + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
        header.Element(Ns + "EndDate")!.Value.Should().Be("2026-12-31");
    }

    /// <summary>
    /// NoStock has to say the same thing as the lines that follow, so it is derived rather than
    /// taken from the caller.
    /// </summary>
    [Fact]
    public void Build_says_there_is_no_stock_when_there_are_no_lines()
    {
        Root(new InventoryFile { Header = Header() })
            .Element(Ns + "StockHeader")!
            .Element(Ns + "NoStock")!.Value.Should().Be("true");
    }

    [Fact]
    public void Build_says_there_is_stock_when_there_are_lines()
    {
        var file = new InventoryFile { Header = Header(), Lines = [Line()] };

        Root(file).Element(Ns + "StockHeader")!.Element(Ns + "NoStock")!.Value.Should().Be("false");
    }

    [Fact]
    public void Build_writes_the_line_fields_in_schema_order()
    {
        var file = new InventoryFile { Header = Header(), Lines = [Line()] };

        Root(file).Element(Ns + "Stock")!.Elements().Select(x => x.Name.LocalName).Should().ContainInOrder(
            "ProductCategory",
            "ProductCode",
            "ProductDescription",
            "ProductNumberCode",
            "ClosingStockQuantity",
            "UnitOfMeasure");
    }

    [Fact]
    public void Build_writes_one_stock_element_per_product()
    {
        var file = new InventoryFile { Header = Header(), Lines = [Line(), Line("ART002", 5m)] };

        Root(file).Elements(Ns + "Stock").Should().HaveCount(2);
    }

    /// <summary>A product with no barcode falls back to its code, which the field allows.</summary>
    [Fact]
    public void Build_falls_back_to_the_product_code_when_there_is_no_barcode()
    {
        var file = new InventoryFile
        {
            Header = Header(),
            Lines =
            [
                new InventoryFileLine
                {
                    ProductCode = "ART001",
                    ProductDescription = "Artigo de teste",
                    ProductNumberCode = string.Empty,
                    ClosingStockQuantity = 10m
                }
            ]
        };

        Root(file).Element(Ns + "Stock")!.Element(Ns + "ProductNumberCode")!.Value.Should().Be("ART001");
    }

    [Fact]
    public void Build_writes_quantities_with_an_invariant_decimal_point()
    {
        var file = new InventoryFile { Header = Header(), Lines = [Line(quantity: 12.5m)] };

        Root(file).Element(Ns + "Stock")!.Element(Ns + "ClosingStockQuantity")!.Value.Should().Be("12.5");
    }

    [Fact]
    public void BuildFileName_carries_the_tax_id_and_the_reference_date()
    {
        InventoryXmlWriter.BuildFileName(Header()).Should().Be("Inventario_500123456_20261231.xml");
    }

    [Fact]
    public void Serialize_writes_utf8_without_a_byte_order_mark()
    {
        var bytes = InventoryXmlWriter.Serialize(new InventoryFile { Header = Header() });

        bytes.Should().StartWith("<?xml"u8.ToArray());
    }
}
