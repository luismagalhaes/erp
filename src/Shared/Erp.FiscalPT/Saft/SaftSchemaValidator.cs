using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Erp.FiscalPT.Saft;

/// <summary>
/// Validates a SAF-T (PT) file against the official schema, which ships embedded in this assembly
/// so validation never depends on the network. Downloaded from
/// <c>https://info.portaldasfinancas.gov.pt/apps/saft-pt04/saftpt1.04_01.xsd</c>.
/// </summary>
/// <remarks>
/// The published schema is XSD 1.1 and .NET only implements 1.0, so the 19 <c>xs:assert</c> rules
/// are dropped when the schema is loaded — see <see cref="SkippedAssertions"/>. Everything else is
/// checked: element order, cardinality, data types, lengths and enumerations. The assertions are
/// co-occurrence rules (a tax exemption reason being required when the tax amount is zero, for
/// one), so passing here is necessary but not sufficient: the tax authority's own validator is
/// still the last word.
/// </remarks>
public static class SaftSchemaValidator
{
    private const string ResourceName = "Erp.FiscalPT.Saft.Schemas.SAFTPT1.04_01.xsd";

    private static readonly XNamespace XsdNamespace = "http://www.w3.org/2001/XMLSchema";

    private static readonly Lazy<XmlSchemaSet> Schemas = new(LoadSchemas);

    private static int _skippedAssertions;

    /// <summary>How many XSD 1.1 assertions were dropped, so a caller can report the gap honestly.</summary>
    public static int SkippedAssertions
    {
        get
        {
            _ = Schemas.Value;
            return _skippedAssertions;
        }
    }

    /// <summary>
    /// Validates the document and returns every problem found. An empty list means the file
    /// conforms to the schema.
    /// </summary>
    public static IReadOnlyList<string> Validate(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();

        document.Validate(Schemas.Value, (_, args) =>
            errors.Add($"{args.Severity} at line {args.Exception?.LineNumber ?? 0}: {args.Message}"));

        return errors;
    }

    /// <summary>Validates a serialized file, as it would be handed to the tax authority.</summary>
    public static IReadOnlyList<string> Validate(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var stream = new MemoryStream(content);
        return Validate(XDocument.Load(stream));
    }

    private static XmlSchemaSet LoadSchemas()
    {
        using var stream = typeof(SaftSchemaValidator).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{ResourceName}' was not found.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // The published schema declares Windows-1252, a code page .NET does not carry by default.
        // The file uses no byte where Windows-1252 differs from Latin-1, so decoding it as Latin-1
        // is exact, and reading from a TextReader makes the declared encoding irrelevant.
        var schemaDocument = XDocument.Parse(Encoding.Latin1.GetString(buffer.ToArray()));

        // .NET implements XSD 1.0, which has no xs:assert. Removing the assertions is what lets the
        // rest of the schema — order, cardinality, types, enumerations — be enforced at all.
        var assertions = schemaDocument.Descendants(XsdNamespace + "assert").ToList();
        _skippedAssertions = assertions.Count;
        assertions.ForEach(assertion => assertion.Remove());

        // XSD 1.1 lets an xs:all repeat its particles; 1.0 does not. The schema has exactly one,
        // inside GeneralLedgerEntries — the accounting file, which a billing export never carries.
        // Turning it into a sequence only adds an ordering constraint, so nothing invalid slips by.
        foreach (var all in schemaDocument.Descendants(XsdNamespace + "all").ToList())
            all.Name = XsdNamespace + "sequence";

        using var xmlReader = schemaDocument.CreateReader();

        var schemas = new XmlSchemaSet();
        schemas.Add(XmlSchema.Read(xmlReader, null)
            ?? throw new InvalidOperationException("The SAF-T schema could not be read."));
        schemas.Compile();

        return schemas;
    }
}
