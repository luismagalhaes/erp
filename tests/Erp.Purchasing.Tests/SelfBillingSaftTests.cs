using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.Saft;
using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.SeriesRegistry.Domain;
using FluentAssertions;
using NSubstitute;

namespace Erp.Purchasing.Tests;

/// <summary>
/// The self-billing SAF-T, from the documents through to the official schema.
/// </summary>
/// <remarks>
/// The point being checked is that this is a <b>different file</b>, not a different block: type
/// <c>"S"</c>, the supplier in the header, us in the <c>Customer</c> table with the self-billing
/// indicator set, and one file per supplier.
/// </remarks>
public class SelfBillingSaftTests
{
    private static readonly XNamespace Ns = SaftConstants.Namespace;

    private readonly ISelfBilledInvoiceStorage _invoices = Substitute.For<ISelfBilledInvoiceStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly List<SelfBilledInvoice> _issued = [];

    private static readonly DateOnly Start = new(2026, 3, 1);
    private static readonly DateOnly End = new(2026, 3, 31);

    /// <summary>Us: the entity that self-bills, and therefore the customer of these sales.</summary>
    private static readonly SaftEntityInfo SelfBiller =
        new("Empresa Teste", "500123456", "Empresa Teste, Lda", "Rua Um", "Lisboa", "1000-001");

    /// <summary>The supplier: whose sales these are, and whose file this is.</summary>
    private static readonly SaftEntityInfo Supplier =
        new("Fornecedor Teste", "501234567", "Fornecedor Teste, Lda", "Rua Dois", "Porto", "4000-002");

    private static readonly SaftProducerInfo Producer =
        new("500123456", "9999", "ErpPortugal/ErpPortugal");

    public SelfBillingSaftTests()
    {
        _invoices.GetForPeriodAsync(
                Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var supplierTaxId = call.ArgAt<string?>(3);

                return (IReadOnlyList<SelfBilledInvoice>)
                [
                    .. _issued.Where(x => supplierTaxId == null || x.Supplier.TaxId == supplierTaxId)
                ];
            });
    }

    private SaftExporter CreateExporter() => new([new SelfBillingSaftSource(_invoices)]);

    private SaftExportSpec Spec(SaftEntityInfo? subject = null, SaftEntityInfo? selfBiller = null) =>
        new(_companyId,
            Start,
            End,
            SaftFileType.SelfBilling,
            subject ?? Supplier,
            Producer,
            selfBiller ?? SelfBiller);

    private readonly Dictionary<string, Series> _series = [];

    private Series SelfBillingSeries(string supplierTaxId)
    {
        if (_series.TryGetValue(supplierTaxId, out var existing))
            return existing;

        var series = new Series
        {
            CompanyId = _companyId,
            DocumentType = SalesDocumentTypes.Invoice,
            SeriesCode = $"AF{supplierTaxId}",
            SelfBilling = true
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _series[supplierTaxId] = series;

        return series;
    }

    private SelfBilledInvoice GivenSelfBilledInvoice(
        string supplierTaxId = "501234567",
        string supplierName = "Fornecedor Teste, Lda",
        decimal net = 200m,
        decimal tax = 46m,
        string productCode = "ART001",
        string userId = "user-1")
    {
        var series = SelfBillingSeries(supplierTaxId);
        var sequence = series.TakeNextSequence();

        var line = new SelfBilledInvoiceLine
        {
            LineNumber = 1,
            ProductCode = productCode,
            ProductDescription = "Artigo de teste",
            Quantity = 2,
            UnitPrice = 100m,
            LineAmount = net,
            TaxCountryRegion = "PT",
            TaxCode = TaxCodes.Normal,
            TaxPercentage = 23m,
            TaxAmount = tax
        };

        var summary = new SelfBilledInvoiceTaxSummary
        {
            TaxCountryRegion = "PT",
            TaxCode = TaxCodes.Normal,
            TaxPercentage = 23m,
            TaxableBase = net,
            TaxAmount = tax
        };

        var invoice = SelfBilledInvoice.Issue(
            _companyId,
            Guid.NewGuid(),
            series,
            sequence,
            $"FT {series.SeriesCode}/{sequence}",
            $"JFTX7RK9-{sequence}",
            new DateOnly(2026, 3, 15),
            new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            new SupplierSnapshot("F001", supplierName, supplierTaxId, "Rua Dois", "4000-002", "Porto"),
            [line],
            [summary],
            net,
            tax,
            net + tax,
            new string('x', 44),
            string.Empty,
            "1",
            userId);

        _issued.Add(invoice);

        return invoice;
    }

    private static XElement Parse(SaftExportOutcome result) =>
        XDocument.Parse(Encoding.UTF8.GetString(result.Content)).Root!;

    [Fact]
    public async Task ExportAsync_declares_the_file_as_self_billing()
    {
        GivenSelfBilledInvoice();

        var header = Parse(await CreateExporter().ExportAsync(Spec())).Element(Ns + "Header")!;

        header.Element(Ns + "TaxAccountingBasis")!.Value.Should().Be("S");
    }

    /// <summary>
    /// The header carries the <b>supplier's</b> tax id, not ours: the documents are their sales,
    /// and we only issued them in their name.
    /// </summary>
    [Fact]
    public async Task ExportAsync_puts_the_supplier_in_the_header()
    {
        GivenSelfBilledInvoice();

        var header = Parse(await CreateExporter().ExportAsync(Spec())).Element(Ns + "Header")!;

        header.Element(Ns + "TaxRegistrationNumber")!.Value.Should().Be("501234567");
        header.Element(Ns + "CompanyName")!.Value.Should().Be("Fornecedor Teste, Lda");
        // The producer is still us: it is our program that signed them.
        header.Element(Ns + "ProductCompanyTaxID")!.Value.Should().Be("500123456");
    }

    /// <summary>
    /// The part that reads backwards until you look from the right side: the customer of a sale of
    /// the supplier's is us, and the indicator says we were the one who billed it.
    /// </summary>
    [Fact]
    public async Task ExportAsync_puts_the_self_biller_in_the_customer_table()
    {
        GivenSelfBilledInvoice();

        var masterFiles = Parse(await CreateExporter().ExportAsync(Spec())).Element(Ns + "MasterFiles")!;

        var customer = masterFiles.Elements(Ns + "Customer").Should().ContainSingle().Subject;
        customer.Element(Ns + "CustomerTaxID")!.Value.Should().Be("500123456");
        customer.Element(Ns + "SelfBillingIndicator")!.Value.Should().Be("1");
    }

    [Fact]
    public async Task ExportAsync_marks_every_document_as_self_billed()
    {
        GivenSelfBilledInvoice();

        var invoice = Parse(await CreateExporter().ExportAsync(Spec()))
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!;

        invoice.Element(Ns + "SpecialRegimes")!
            .Element(Ns + "SelfBillingIndicator")!.Value.Should().Be("1");
        invoice.Element(Ns + "CustomerID")!.Value.Should().Be("500123456");
        invoice.Element(Ns + "ATCUD")!.Value.Should().Be("JFTX7RK9-1");
    }

    /// <summary>
    /// One file per supplier. A file holding two suppliers' sales would have a header that is true
    /// of only one of them.
    /// </summary>
    [Fact]
    public async Task ExportAsync_leaves_out_the_documents_of_other_suppliers()
    {
        GivenSelfBilledInvoice(supplierTaxId: "501234567");
        GivenSelfBilledInvoice(supplierTaxId: "502222222", supplierName: "Outro Fornecedor");

        var invoices = Parse(await CreateExporter().ExportAsync(Spec()))
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Elements(Ns + "Invoice")
            .ToList();

        invoices.Should().ContainSingle();
        invoices[0].Element(Ns + "InvoiceNo")!.Value.Should().Be("FT AF501234567/1");
    }

    [Fact]
    public async Task ExportAsync_keeps_a_voided_document_in_the_file_with_its_reason()
    {
        var invoice = GivenSelfBilledInvoice();
        invoice.Void("Erro de faturação", "user-2", new DateTime(2026, 3, 16, 11, 0, 0, DateTimeKind.Utc));

        var status = Parse(await CreateExporter().ExportAsync(Spec()))
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "DocumentStatus")!;

        status.Element(Ns + "InvoiceStatus")!.Value.Should().Be("A");
        status.Element(Ns + "Reason")!.Value.Should().Be("Erro de faturação");
        status.Element(Ns + "SourceID")!.Value.Should().Be("user-2");
    }

    [Fact]
    public async Task ExportAsync_names_the_file_after_the_supplier_and_the_period()
    {
        GivenSelfBilledInvoice();

        var result = await CreateExporter().ExportAsync(Spec());

        result.FileName.Should().Be("SAFT_501234567_20260301_20260331.xml");
    }

    [Fact]
    public async Task ExportAsync_refuses_a_self_billing_file_with_no_self_biller()
    {
        GivenSelfBilledInvoice();

        var spec = Spec() with { SelfBiller = null };

        var act = () => CreateExporter().ExportAsync(spec);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*self-biller*");
    }

    /// <summary>
    /// The end to end check: what comes out of real documents has to pass the tax authority's own
    /// schema, not merely look right.
    /// </summary>
    [Fact]
    public async Task ExportAsync_produces_a_file_that_passes_the_official_schema()
    {
        GivenSelfBilledInvoice();
        GivenSelfBilledInvoice(productCode: "ART002");
        GivenSelfBilledInvoice().Void("Erro de faturação", "user-2", new DateTime(2026, 3, 16, 11, 0, 0, DateTimeKind.Utc));

        var result = await CreateExporter().ExportAsync(Spec());

        result.ValidationErrors.Should().BeEmpty();
        SaftSchemaValidator.Validate(result.Content).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_validates_a_supplier_with_no_documents_too()
    {
        var result = await CreateExporter().ExportAsync(Spec());

        result.InvoiceCount.Should().Be(0);
        result.ValidationErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The billing exporter and this one share everything but the file type, and the file type is
    /// what keeps these documents out of the billing file.
    /// </summary>
    [Fact]
    public async Task ExportAsync_puts_nothing_of_this_module_in_the_billing_file()
    {
        GivenSelfBilledInvoice();

        var billing = Spec() with { FileType = SaftFileType.Billing, Subject = SelfBiller, SelfBiller = null };

        var result = await CreateExporter().ExportAsync(billing);

        result.InvoiceCount.Should().Be(0);
    }
}
