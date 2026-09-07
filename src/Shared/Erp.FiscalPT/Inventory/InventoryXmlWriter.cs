using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Erp.FiscalPT.Inventory;

/// <summary>
/// Writes the inventory communication file, following the official schemas: a StockHeader followed
/// by one Stock element per product held. The two versions differ only in the namespace, the
/// version string and whether each line carries its value.
/// </summary>
public static class InventoryXmlWriter
{
    public static XDocument Build(InventoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        XNamespace ns = InventoryConstants.Namespace(file.Version);

        var stockFile = new XElement(ns + "StockFile", BuildHeader(file, ns));

        foreach (var line in file.Lines)
        {
            var stock = new XElement(ns + "Stock",
                new XElement(ns + "ProductCategory", line.ProductCategory),
                new XElement(ns + "ProductCode", line.ProductCode),
                new XElement(ns + "ProductDescription", line.ProductDescription),
                new XElement(ns + "ProductNumberCode", Fallback(line.ProductNumberCode, line.ProductCode)),
                new XElement(ns + "ClosingStockQuantity", Quantity(line.ClosingStockQuantity)),
                new XElement(ns + "UnitOfMeasure", line.UnitOfMeasure));

            // The quantities-only schema has no such element and would reject it.
            if (InventoryConstants.CarriesValue(file.Version))
                stock.Add(new XElement(ns + "ClosingStockValue", Money(line.ClosingStockValue)));

            stockFile.Add(stock);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), stockFile);
    }

    /// <summary>Serializes the file as UTF-8 bytes, without a byte order mark.</summary>
    public static byte[] Serialize(InventoryFile file)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            Build(file).Save(writer, SaveOptions.None);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// File name in the shape the tax authority uses: tax id and reference date, e.g.
    /// <c>Inventario_123456789_20261231.xml</c>.
    /// </summary>
    public static string BuildFileName(InventoryFileHeader header) =>
        $"Inventario_{header.TaxRegistrationNumber}_{header.EndDate:yyyyMMdd}.xml";

    /// <summary>
    /// The header. <c>NoStock</c> is derived rather than taken from the caller: it has to say the
    /// same thing as the lines that follow, and deriving it is the only way it always does.
    /// </summary>
    private static XElement BuildHeader(InventoryFile file, XNamespace ns) =>
        new(ns + "StockHeader",
            new XElement(ns + "FileVersion", InventoryConstants.FileVersion(file.Version)),
            new XElement(ns + "TaxRegistrationNumber", file.Header.TaxRegistrationNumber),
            new XElement(ns + "FiscalYear", file.Header.FiscalYear),
            new XElement(ns + "EndDate", Date(file.Header.EndDate)),
            new XElement(ns + "NoStock", file.Lines.Count == 0 ? "true" : "false"));

    private static string Fallback(string? value, string alternative) =>
        string.IsNullOrWhiteSpace(value) ? alternative : value.Trim();

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
