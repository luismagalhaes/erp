using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.Saft;
using Erp.Sales.Application.Configuration;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Sales.Tests;

public class SaftExportServiceTests
{
    private static readonly XNamespace Ns = SaftConstants.Namespace;

    private readonly ISalesDocumentStorage _documents = Substitute.For<ISalesDocumentStorage>();
    private readonly IStockMovementStorage _movements = Substitute.For<IStockMovementStorage>();
    private readonly IPaymentStorage _payments = Substitute.For<IPaymentStorage>();
    private readonly Guid _companyId = Guid.NewGuid();

    private readonly List<SalesDocument> _documentsInPeriod = [];
    private readonly List<StockMovement> _movementsInPeriod = [];
    private readonly List<Payment> _paymentsInPeriod = [];

    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 1, 31);

    public SaftExportServiceTests()
    {
        _documents.GetForPeriodAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<SalesDocument>)[.. _documentsInPeriod]);

        _movements.GetForPeriodAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<StockMovement>)[.. _movementsInPeriod]);

        _payments.GetForPeriodAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<Payment>)[.. _paymentsInPeriod]);
    }

    private SaftExportService CreateService() =>
        new(_documents, _movements, _payments,
            Options.Create(new FiscalOptions { IssuerTaxId = "123456789", CertificateNumber = "9999", KeyVersion = "1" }));

    private static SaftCompanyInfo CompanyInfo(string taxId = "500123456") =>
        new("Empresa Teste", taxId, "Empresa Teste, Lda", "Rua Um", "Lisboa", "1000-001");

    private SaftExportRequest Request() => new(_companyId, Start, End, CompanyInfo());

    private readonly Dictionary<string, Series> _series = [];

    /// <summary>
    /// One series per document type, kept across calls: documents of the same type share the
    /// counter, exactly as they do in the database. A series per document would repeat the
    /// numbering, which the schema rejects as a duplicate key.
    /// </summary>
    private Series IssuedSeries(string documentType)
    {
        if (_series.TryGetValue(documentType, out var existing))
            return existing;

        var series = new Series { CompanyId = _companyId, DocumentType = documentType, SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _series[documentType] = series;

        return series;
    }

    private SalesDocument GivenInvoice(
        string documentType = "FT",
        string customerTaxId = "500999999",
        string customerName = "Cliente Teste",
        decimal net = 200m,
        decimal tax = 46m,
        string productCode = "ART001")
    {
        var series = IssuedSeries(documentType);
        var sequence = series.TakeNextSequence();

        var line = new SalesDocumentLine
        {
            LineNumber = 1,
            ProductCode = productCode,
            ProductDescription = "Artigo de teste",
            Quantity = 2,
            UnitPrice = 100m,
            LineAmount = net,
            TaxCountryRegion = "PT",
            TaxCode = "NOR",
            TaxPercentage = 23m,
            TaxAmount = tax
        };

        var document = SalesDocument.Issue(
            _companyId,
            series,
            sequence,
            $"{documentType} A2026/{sequence}",
            $"JFTX7RK9-{sequence}",
            new DateOnly(2026, 1, 15),
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            new CustomerSnapshot(customerTaxId, customerName, "Rua do Cliente"),
            [line],
            [],
            net,
            tax,
            net + tax,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1");

        _documentsInPeriod.Add(document);

        return document;
    }

    private Payment GivenPayment(SalesDocument invoice)
    {
        var series = IssuedSeries("RG");
        var sequence = series.TakeNextSequence();

        var payment = Payment.Issue(
            _companyId,
            series,
            sequence,
            $"RG A2026/{sequence}",
            $"JFTX7RK9-{sequence}",
            new DateOnly(2026, 1, 20),
            new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc),
            new PaymentParty(invoice.CustomerTaxId, invoice.CustomerName),
            null,
            [
                new PaymentLine
                {
                    LineNumber = 1,
                    OriginatingDocumentId = invoice.Id,
                    OriginatingNumber = invoice.DocumentNumber,
                    OriginatingDate = invoice.DocumentDate,
                    AppliedAmount = invoice.GrossTotal
                }
            ],
            [new PaymentMethodEntry { Mechanism = "TB", Amount = invoice.GrossTotal, PaymentDate = new DateOnly(2026, 1, 20) }],
            invoice.GrossTotal,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1");

        _paymentsInPeriod.Add(payment);

        return payment;
    }

    private static XElement Parse(SaftExportResult result) =>
        XDocument.Parse(Encoding.UTF8.GetString(result.Content)).Root!;

    [Fact]
    public async Task ExportAsync_names_the_file_after_the_entity_and_the_period()
    {
        GivenInvoice();

        var result = await CreateService().ExportAsync(Request());

        result.FileName.Should().Be("SAFT_500123456_20260101_20260131.xml");
    }

    [Fact]
    public async Task ExportAsync_puts_the_taxable_entity_in_the_header()
    {
        GivenInvoice();

        var header = Parse(await CreateService().ExportAsync(Request())).Element(Ns + "Header")!;

        header.Element(Ns + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
        header.Element(Ns + "CompanyName")!.Value.Should().Be("Empresa Teste, Lda");
        header.Element(Ns + "SoftwareCertificateNumber")!.Value.Should().Be("9999");
        header.Element(Ns + "ProductCompanyTaxID")!.Value.Should().Be("123456789");
    }

    [Fact]
    public async Task ExportAsync_strips_non_digits_from_the_tax_registration_number()
    {
        GivenInvoice();

        var request = new SaftExportRequest(_companyId, Start, End, CompanyInfo("PT 500 123 456"));

        var header = Parse(await CreateService().ExportAsync(request)).Element(Ns + "Header")!;

        header.Element(Ns + "TaxRegistrationNumber")!.Value.Should().Be("500123456");
    }

    [Fact]
    public async Task ExportAsync_derives_the_customers_from_the_documents()
    {
        GivenInvoice(customerTaxId: "500999999", customerName: "Cliente A");
        GivenInvoice(customerTaxId: "500888888", customerName: "Cliente B");
        GivenInvoice(customerTaxId: "500999999", customerName: "Cliente A");

        var masterFiles = Parse(await CreateService().ExportAsync(Request())).Element(Ns + "MasterFiles")!;

        var customers = masterFiles.Elements(Ns + "Customer").ToList();
        customers.Should().HaveCount(2);
        customers.Select(x => x.Element(Ns + "CustomerID")!.Value)
            .Should().BeEquivalentTo(["500888888", "500999999"]);
    }

    [Fact]
    public async Task ExportAsync_derives_the_products_from_the_lines()
    {
        GivenInvoice(productCode: "ART001");
        GivenInvoice(productCode: "ART002");
        GivenInvoice(productCode: "ART001");

        var masterFiles = Parse(await CreateService().ExportAsync(Request())).Element(Ns + "MasterFiles")!;

        masterFiles.Elements(Ns + "Product").Select(x => x.Element(Ns + "ProductCode")!.Value)
            .Should().BeEquivalentTo(["ART001", "ART002"]);
    }

    [Fact]
    public async Task ExportAsync_declares_only_the_tax_rates_the_documents_use()
    {
        GivenInvoice();
        GivenInvoice();

        var taxTable = Parse(await CreateService().ExportAsync(Request()))
            .Element(Ns + "MasterFiles")!
            .Element(Ns + "TaxTable")!;

        var entry = taxTable.Elements(Ns + "TaxTableEntry").Should().ContainSingle().Subject;
        entry.Element(Ns + "TaxCode")!.Value.Should().Be("NOR");
        entry.Element(Ns + "TaxPercentage")!.Value.Should().Be("23.00");
        entry.Element(Ns + "Description")!.Value.Should().Be("Taxa normal");
    }

    [Fact]
    public async Task ExportAsync_keeps_a_voided_document_in_the_file_with_its_reason()
    {
        var invoice = GivenInvoice();
        invoice.Void("Erro de faturação", "user-2", new DateTime(2026, 1, 16, 11, 0, 0, DateTimeKind.Utc));

        var status = Parse(await CreateService().ExportAsync(Request()))
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "DocumentStatus")!;

        status.Element(Ns + "InvoiceStatus")!.Value.Should().Be("A");
        status.Element(Ns + "Reason")!.Value.Should().Be("Erro de faturação");
        status.Element(Ns + "InvoiceStatusDate")!.Value.Should().Be("2026-01-16T11:00:00");
        status.Element(Ns + "SourceID")!.Value.Should().Be("user-2");
    }

    [Fact]
    public async Task ExportAsync_carries_the_signature_of_every_document()
    {
        GivenInvoice();

        var invoice = Parse(await CreateService().ExportAsync(Request()))
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!;

        invoice.Element(Ns + "Hash")!.Value.Should().Be(new string('x', 44));
        invoice.Element(Ns + "HashControl")!.Value.Should().Be("1");
        invoice.Element(Ns + "ATCUD")!.Value.Should().Be("JFTX7RK9-1");
        invoice.Element(Ns + "Period")!.Value.Should().Be("1");
    }

    [Fact]
    public async Task ExportAsync_includes_the_receipts_of_the_period()
    {
        var invoice = GivenInvoice();
        GivenPayment(invoice);

        var result = await CreateService().ExportAsync(Request());

        result.PaymentCount.Should().Be(1);

        var payment = Parse(result).Element(Ns + "SourceDocuments")!
            .Element(Ns + "Payments")!
            .Element(Ns + "Payment")!;

        payment.Element(Ns + "PaymentRefNo")!.Value.Should().Be("RG A2026/1");
        payment.Element(Ns + "Line")!
            .Element(Ns + "SourceDocumentID")!
            .Element(Ns + "OriginatingON")!.Value.Should().Be(invoice.DocumentNumber);
    }

    /// <summary>
    /// The end to end check: whatever the service builds from real documents has to pass the
    /// tax authority's own schema, not merely look right.
    /// </summary>
    [Fact]
    public async Task ExportAsync_produces_a_file_that_passes_the_official_schema()
    {
        var invoice = GivenInvoice();
        GivenInvoice(documentType: "NC", customerTaxId: "500888888", customerName: "Outro Cliente");
        GivenInvoice().Void("Erro de faturação", "user-2", new DateTime(2026, 1, 16, 11, 0, 0, DateTimeKind.Utc));
        GivenPayment(invoice);

        var result = await CreateService().ExportAsync(Request());

        result.ValidationErrors.Should().BeEmpty();
        SaftSchemaValidator.Validate(result.Content).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_validates_an_empty_period_too()
    {
        var result = await CreateService().ExportAsync(Request());

        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_rejects_a_company_without_a_tax_id()
    {
        var request = new SaftExportRequest(_companyId, Start, End, CompanyInfo(taxId: "   "));

        var act = () => CreateService().ExportAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*tax id*");
    }

    [Fact]
    public async Task ExportAsync_rejects_a_period_that_ends_before_it_starts()
    {
        var request = new SaftExportRequest(_companyId, End, Start, CompanyInfo());

        var act = () => CreateService().ExportAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*end date*");
    }

    [Fact]
    public async Task GetSummaryAsync_counts_the_documents_and_totals_the_ones_that_stand()
    {
        var invoice = GivenInvoice();
        GivenInvoice().Void("Erro", "user-2", DateTime.UtcNow);
        GivenPayment(invoice);

        var summary = await CreateService().GetSummaryAsync(_companyId, Start, End);

        summary.InvoiceCount.Should().Be(2);
        summary.PaymentCount.Should().Be(1);
        summary.InvoiceGrossTotal.Should().Be(246m);
        summary.PaymentGrossTotal.Should().Be(246m);
    }
}
