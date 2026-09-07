using System.Xml.Linq;
using Erp.FiscalPT.Saft;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class SaftXmlWriterTests
{
    private static readonly XNamespace Ns = SaftConstants.Namespace;

    private static SaftHeader Header() => new()
    {
        CompanyId = "500123456",
        TaxRegistrationNumber = "500123456",
        CompanyName = "Empresa Teste, Lda",
        CompanyAddress = new SaftAddress { AddressDetail = "Rua Um", City = "Lisboa", PostalCode = "1000-001" },
        FiscalYear = 2026,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 1, 31),
        DateCreated = new DateOnly(2026, 2, 1),
        ProductCompanyTaxId = "123456789",
        SoftwareCertificateNumber = "9999",
        ProductId = "ErpPortugal/ErpPortugal"
    };

    private static SaftInvoice Invoice(
        string invoiceNo = "FT A2026/1",
        string invoiceType = "FT",
        string status = "N",
        decimal net = 200m,
        decimal tax = 46m) => new()
    {
        InvoiceNo = invoiceNo,
        Atcud = "JFTX7RK9-1",
        InvoiceType = invoiceType,
        DocumentStatus = new SaftDocumentStatus
        {
            Status = status,
            StatusDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Reason = status == "A" ? "Erro de faturação" : null,
            SourceId = "user-1"
        },
        Hash = new string('x', 172),
        HashControl = "1",
        Period = 1,
        InvoiceDate = new DateOnly(2026, 1, 15),
        SystemEntryDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        CustomerId = "500999999",
        Lines =
        [
            new SaftInvoiceLine
            {
                LineNumber = 1,
                ProductCode = "ART001",
                ProductDescription = "Artigo de teste",
                Quantity = 2,
                UnitPrice = 100m,
                TaxPointDate = new DateOnly(2026, 1, 15),
                Description = "Artigo de teste",
                Amount = net,
                Tax = new SaftTax { TaxCode = "NOR", TaxPercentage = 23m }
            }
        ],
        Totals = new SaftDocumentTotals { TaxPayable = tax, NetTotal = net, GrossTotal = net + tax }
    };

    private static XElement Root(SaftAuditFile file) => SaftXmlWriter.Build(file).Root!;

    [Fact]
    public void Build_uses_the_official_namespace_and_version()
    {
        var root = Root(new SaftAuditFile { Header = Header() });

        root.Name.Should().Be(Ns + "AuditFile");
        root.Element(Ns + "Header")!.Element(Ns + "AuditFileVersion")!.Value.Should().Be("1.04_01");
    }

    [Fact]
    public void Build_writes_the_header_elements_in_schema_order()
    {
        var root = Root(new SaftAuditFile { Header = Header() });

        var names = root.Element(Ns + "Header")!.Elements().Select(x => x.Name.LocalName).ToList();

        // Order matters: the tax authority validates the file against the XSD.
        names.Should().ContainInOrder(
            "AuditFileVersion", "CompanyID", "TaxRegistrationNumber", "TaxAccountingBasis", "CompanyName",
            "CompanyAddress", "FiscalYear", "StartDate", "EndDate", "CurrencyCode", "DateCreated",
            "TaxEntity", "ProductCompanyTaxID", "SoftwareCertificateNumber", "ProductID", "ProductVersion");
    }

    /// <summary>An empty TaxTable is invalid — the schema wants at least one entry — so it is omitted.</summary>
    [Fact]
    public void Build_omits_the_tax_table_when_there_are_no_rates()
    {
        var root = Root(new SaftAuditFile { Header = Header() });

        var masterFiles = root.Element(Ns + "MasterFiles")!;
        masterFiles.Should().NotBeNull();
        masterFiles.Element(Ns + "TaxTable").Should().BeNull();
    }

    [Fact]
    public void Build_writes_the_tax_table_when_there_are_rates()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            TaxTable = [new SaftTaxTableEntry { TaxCode = "NOR", Description = "Taxa normal", TaxPercentage = 23m }]
        };

        Root(file).Element(Ns + "MasterFiles")!.Element(Ns + "TaxTable")!
            .Elements(Ns + "TaxTableEntry").Should().ContainSingle();
    }

    [Fact]
    public void Build_leaves_out_document_families_with_nothing_to_report()
    {
        var root = Root(new SaftAuditFile { Header = Header(), Invoices = [Invoice()] });

        var sourceDocuments = root.Element(Ns + "SourceDocuments")!;
        sourceDocuments.Element(Ns + "SalesInvoices").Should().NotBeNull();
        sourceDocuments.Element(Ns + "MovementOfGoods").Should().BeNull();
        sourceDocuments.Element(Ns + "Payments").Should().BeNull();
    }

    [Fact]
    public void Build_counts_every_invoice_including_the_voided_ones()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            Invoices = [Invoice(), Invoice("FT A2026/2", status: "A")]
        };

        var salesInvoices = Root(file).Element(Ns + "SourceDocuments")!.Element(Ns + "SalesInvoices")!;

        salesInvoices.Element(Ns + "NumberOfEntries")!.Value.Should().Be("2");
    }

    [Fact]
    public void Build_leaves_voided_invoices_out_of_the_control_totals()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            Invoices = [Invoice(), Invoice("FT A2026/2", status: "A")]
        };

        var salesInvoices = Root(file).Element(Ns + "SourceDocuments")!.Element(Ns + "SalesInvoices")!;

        salesInvoices.Element(Ns + "TotalCredit")!.Value.Should().Be("200.00");
        salesInvoices.Element(Ns + "TotalDebit")!.Value.Should().Be("0.00");
    }

    [Fact]
    public void Build_puts_credit_notes_on_the_debit_side()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            Invoices = [Invoice("NC A2026/1", "NC")]
        };

        var salesInvoices = Root(file).Element(Ns + "SourceDocuments")!.Element(Ns + "SalesInvoices")!;

        salesInvoices.Element(Ns + "TotalDebit")!.Value.Should().Be("200.00");
        salesInvoices.Element(Ns + "TotalCredit")!.Value.Should().Be("0.00");
        salesInvoices.Element(Ns + "Invoice")!.Element(Ns + "Line")!
            .Element(Ns + "DebitAmount").Should().NotBeNull();
    }

    /// <summary>
    /// Artigo 36.º n.º 5 do CIVA: a rectifying document must name the document it corrects. In the
    /// SAF-T that goes on the line, between TaxPointDate and Description.
    /// </summary>
    [Fact]
    public void Build_writes_the_reference_of_a_rectifying_line()
    {
        var creditNote = Invoice("NC A2026/1", "NC");
        var line = creditNote.Lines[0];

        var file = new SaftAuditFile
        {
            Header = Header(),
            Invoices =
            [
                new SaftInvoice
                {
                    InvoiceNo = creditNote.InvoiceNo,
                    Atcud = creditNote.Atcud,
                    InvoiceType = "NC",
                    DocumentStatus = creditNote.DocumentStatus,
                    Hash = creditNote.Hash,
                    HashControl = creditNote.HashControl,
                    Period = creditNote.Period,
                    InvoiceDate = creditNote.InvoiceDate,
                    SystemEntryDate = creditNote.SystemEntryDate,
                    CustomerId = creditNote.CustomerId,
                    Lines =
                    [
                        new SaftInvoiceLine
                        {
                            LineNumber = line.LineNumber,
                            ProductCode = line.ProductCode,
                            ProductDescription = line.ProductDescription,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            TaxPointDate = line.TaxPointDate,
                            Description = line.Description,
                            Amount = line.Amount,
                            Tax = line.Tax,
                            Reference = "FT A2026/7",
                            ReferenceReason = "Devolução de mercadoria"
                        }
                    ],
                    Totals = creditNote.Totals
                }
            ]
        };

        var element = Root(file).Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "Line")!;

        var references = element.Element(Ns + "References")!;
        references.Element(Ns + "Reference")!.Value.Should().Be("FT A2026/7");
        references.Element(Ns + "Reason")!.Value.Should().Be("Devolução de mercadoria");

        // The schema fixes where it sits, so the order is part of the contract.
        element.Elements().Select(x => x.Name.LocalName)
            .Should().ContainInOrder("TaxPointDate", "References", "Description");
    }

    [Fact]
    public void Build_leaves_out_the_reference_on_a_line_that_corrects_nothing()
    {
        var file = new SaftAuditFile { Header = Header(), Invoices = [Invoice()] };

        Root(file).Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "Line")!
            .Element(Ns + "References").Should().BeNull();
    }

    [Fact]
    public void Build_records_the_reason_of_a_voided_document()
    {
        var file = new SaftAuditFile { Header = Header(), Invoices = [Invoice(status: "A")] };

        var status = Root(file).Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "DocumentStatus")!;

        status.Element(Ns + "InvoiceStatus")!.Value.Should().Be("A");
        status.Element(Ns + "Reason")!.Value.Should().Be("Erro de faturação");
    }

    [Fact]
    public void Build_omits_optional_elements_that_carry_no_value()
    {
        var file = new SaftAuditFile { Header = Header(), Invoices = [Invoice()] };

        var invoice = Root(file).Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!;

        invoice.Element(Ns + "DocumentStatus")!.Element(Ns + "Reason").Should().BeNull();
        invoice.Element(Ns + "Line")!.Element(Ns + "TaxExemptionReason").Should().BeNull();
    }

    [Fact]
    public void Build_writes_amounts_with_an_invariant_decimal_point()
    {
        var file = new SaftAuditFile { Header = Header(), Invoices = [Invoice(net: 1234.5m, tax: 283.94m)] };

        var totals = Root(file).Element(Ns + "SourceDocuments")!
            .Element(Ns + "SalesInvoices")!
            .Element(Ns + "Invoice")!
            .Element(Ns + "DocumentTotals")!;

        totals.Element(Ns + "NetTotal")!.Value.Should().Be("1234.50");
        totals.Element(Ns + "GrossTotal")!.Value.Should().Be("1518.44");
    }

    [Fact]
    public void Build_writes_a_required_address_field_with_the_expected_placeholder()
    {
        var file = new SaftAuditFile
        {
            Header = new SaftHeader { TaxRegistrationNumber = "500123456", CompanyAddress = new SaftAddress() }
        };

        var address = Root(file).Element(Ns + "Header")!.Element(Ns + "CompanyAddress")!;

        address.Element(Ns + "AddressDetail")!.Value.Should().Be("Desconhecido");
        address.Element(Ns + "Country")!.Value.Should().Be("PT");
    }

    [Fact]
    public void Build_names_the_movement_counterparty_by_what_it_is()
    {
        var movement = new SaftStockMovement
        {
            DocumentNumber = "GT A2026/1",
            MovementType = "GT",
            PartyId = "500999999",
            PartyIsSupplier = true,
            MovementStartTime = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            MovementDate = new DateOnly(2026, 1, 15),
            Period = 1
        };

        var element = Root(new SaftAuditFile { Header = Header(), StockMovements = [movement] })
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "MovementOfGoods")!
            .Element(Ns + "StockMovement")!;

        element.Element(Ns + "SupplierID")!.Value.Should().Be("500999999");
        element.Element(Ns + "CustomerID").Should().BeNull();
    }

    [Fact]
    public void Build_totals_the_quantity_issued_of_the_movements_that_stand()
    {
        SaftStockMovement Movement(string number, string status, decimal quantity) => new()
        {
            DocumentNumber = number,
            MovementType = "GT",
            MovementDate = new DateOnly(2026, 1, 15),
            Period = 1,
            MovementStartTime = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            DocumentStatus = new SaftDocumentStatus { Status = status, Reason = status == "A" ? "Erro" : null },
            Lines = [new SaftStockMovementLine { LineNumber = 1, Quantity = quantity }]
        };

        var file = new SaftAuditFile
        {
            Header = Header(),
            StockMovements = [Movement("GT A2026/1", "N", 3), Movement("GT A2026/2", "A", 5)]
        };

        var movements = Root(file).Element(Ns + "SourceDocuments")!.Element(Ns + "MovementOfGoods")!;

        movements.Element(Ns + "NumberOfMovementLines")!.Value.Should().Be("2");
        movements.Element(Ns + "TotalQuantityIssued")!.Value.Should().Be("3");
    }

    [Fact]
    public void Build_writes_the_settled_invoice_of_each_receipt_line()
    {
        var payment = new SaftPayment
        {
            PaymentRefNo = "RG A2026/1",
            PaymentType = "RG",
            Period = 2,
            TransactionDate = new DateOnly(2026, 2, 20),
            CustomerId = "500999999",
            PaymentMethods = [new SaftPaymentMethod { PaymentMechanism = "TB", PaymentAmount = 246m, PaymentDate = new DateOnly(2026, 2, 20) }],
            Lines = [new SaftPaymentLine { LineNumber = 1, OriginatingOn = "FT A2026/1", InvoiceDate = new DateOnly(2026, 1, 15), Amount = 246m }],
            Totals = new SaftDocumentTotals { NetTotal = 246m, GrossTotal = 246m }
        };

        var element = Root(new SaftAuditFile { Header = Header(), Payments = [payment] })
            .Element(Ns + "SourceDocuments")!
            .Element(Ns + "Payments")!
            .Element(Ns + "Payment")!;

        var line = element.Element(Ns + "Line")!;
        line.Element(Ns + "SourceDocumentID")!.Element(Ns + "OriginatingON")!.Value.Should().Be("FT A2026/1");
        line.Element(Ns + "CreditAmount")!.Value.Should().Be("246.00");
    }

    [Fact]
    public void BuildFileName_follows_the_tax_authority_convention()
    {
        SaftXmlWriter.BuildFileName(Header()).Should().Be("SAFT_500123456_20260101_20260131.xml");
    }

    [Fact]
    public void Serialize_writes_utf8_without_a_byte_order_mark()
    {
        var bytes = SaftXmlWriter.Serialize(new SaftAuditFile { Header = Header() });

        bytes.Should().StartWith("<?xml"u8.ToArray());
    }
}
