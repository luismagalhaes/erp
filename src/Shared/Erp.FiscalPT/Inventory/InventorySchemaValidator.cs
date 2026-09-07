using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Erp.FiscalPT.Inventory;

/// <summary>
/// Validates an inventory file against the official schema of its version. Both ship embedded in
/// this assembly, so validation never depends on the network.
/// </summary>
/// <remarks>
/// Unlike the SAF-T schema, these are plain XSD 1.0 with no assertions, so nothing has to be
/// dropped to load them: what passes here passes in full.
/// </remarks>
public static class InventorySchemaValidator
{
    private static readonly Lazy<XmlSchemaSet> QuantitiesOnly =
        new(() => LoadSchemas("Erp.FiscalPT.Inventory.Schemas.Stock_1_2.xsd"));

    private static readonly Lazy<XmlSchemaSet> Valued =
        new(() => LoadSchemas("Erp.FiscalPT.Inventory.Schemas.Inventario_2_01.xsd"));

    /// <summary>
    /// Validates the document against the schema of the given version, and returns every problem
    /// found. An empty list means the file conforms.
    /// </summary>
    public static IReadOnlyList<string> Validate(XDocument document, InventoryFileVersion version)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();

        // A root the schema has no declaration for is validated laxly, which means silently: a file
        // built for one version and checked against the other would come back clean. The root is
        // the one thing worth asserting outright.
        var expected = XNamespace.Get(InventoryConstants.Namespace(version)) + "StockFile";

        if (document.Root?.Name != expected)
        {
            errors.Add(
                $"Error at line 0: the root element is '{document.Root?.Name.ToString() ?? "(none)"}' " +
                $"but version {InventoryConstants.FileVersion(version)} requires '{expected}'.");

            return errors;
        }

        document.Validate(SchemasFor(version), (_, args) =>
            errors.Add($"{args.Severity} at line {args.Exception?.LineNumber ?? 0}: {args.Message}"));

        return errors;
    }

    /// <summary>Validates a serialized file, as it would be handed to the tax authority.</summary>
    public static IReadOnlyList<string> Validate(byte[] content, InventoryFileVersion version)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var stream = new MemoryStream(content);
        return Validate(XDocument.Load(stream), version);
    }

    private static XmlSchemaSet SchemasFor(InventoryFileVersion version) => version switch
    {
        InventoryFileVersion.QuantitiesOnly => QuantitiesOnly.Value,
        InventoryFileVersion.Valued => Valued.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    private static XmlSchemaSet LoadSchemas(string resourceName)
    {
        using var stream = typeof(InventorySchemaValidator).GetTypeInfo().Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{resourceName}' was not found.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // The published schema declares Windows-1252, a code page .NET does not carry by default.
        // Reading from a TextReader makes the declared encoding irrelevant; Latin-1 covers the
        // accented characters in the documentation text.
        using var reader = new StringReader(Encoding.Latin1.GetString(buffer.ToArray()));
        using var xmlReader = XmlReader.Create(reader);

        var schemas = new XmlSchemaSet();
        schemas.Add(XmlSchema.Read(xmlReader, null)
            ?? throw new InvalidOperationException("The inventory schema could not be read."));
        schemas.Compile();

        return schemas;
    }
}
