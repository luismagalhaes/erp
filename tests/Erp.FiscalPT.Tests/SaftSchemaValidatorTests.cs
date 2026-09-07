using System.Xml.Linq;
using Erp.FiscalPT.Saft;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

/// <summary>
/// Validates against the official XSD from the tax authority. These are the tests that say whether
/// a file would be accepted, rather than whether it merely looks right.
/// </summary>
public class SaftSchemaValidatorTests
{
    private static SaftHeader Header() => new()
    {
        CompanyId = "500123456",
        TaxRegistrationNumber = "500123456",
        CompanyName = "Empresa Teste, Lda",
        BusinessName = "Empresa Teste",
        CompanyAddress = new SaftAddress { AddressDetail = "Rua Um, 10", City = "Lisboa", PostalCode = "1000-001" },
        FiscalYear = 2026,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 1, 31),
        DateCreated = new DateOnly(2026, 2, 1),
        ProductCompanyTaxId = "123456789",
        SoftwareCertificateNumber = "9999",
        ProductId = "ErpPortugal/ErpPortugal",
        ProductVersion = "1.0",
        Telephone = "210000000",
        Email = "geral@empresa.pt"
    };

    private static SaftCustomer Customer(string taxId = "500999999") => new()
    {
        CustomerId = taxId,
        CustomerTaxId = taxId,
        CompanyName = "Cliente Teste",
        BillingAddress = new SaftAddress { AddressDetail = "Rua do Cliente", City = "Porto", PostalCode = "4000-001" }
    };

    private static SaftProduct Product(string code = "ART001") => new()
    {
        ProductCode = code,
        ProductDescription = "Artigo de teste",
        ProductNumberCode = code
    };

    private static SaftTaxTableEntry TaxEntry() => new()
    {
        TaxCode = "NOR",
        Description = "Taxa normal",
        TaxPercentage = 23m
    };

    private static SaftTax Tax() => new() { TaxCode = "NOR", TaxPercentage = 23m };

    private static SaftInvoice Invoice(string invoiceNo = "FT A2026/1", string invoiceType = "FT", string status = "N") => new()
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
        SourceId = "user-1",
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
                UnitOfMeasure = "UN",
                UnitPrice = 100m,
                TaxPointDate = new DateOnly(2026, 1, 15),
                Description = "Artigo de teste",
                Amount = 200m,
                Tax = Tax()
            }
        ],
        Totals = new SaftDocumentTotals { TaxPayable = 46m, NetTotal = 200m, GrossTotal = 246m }
    };

    private static SaftStockMovement StockMovement() => new()
    {
        DocumentNumber = "GT A2026/1",
        Atcud = "JFTX7RK9-1",
        DocumentStatus = new SaftDocumentStatus
        {
            StatusDate = new DateTime(2026, 1, 16, 8, 0, 0, DateTimeKind.Utc),
            SourceId = "user-1"
        },
        Hash = new string('x', 172),
        HashControl = "1",
        Period = 1,
        MovementDate = new DateOnly(2026, 1, 16),
        MovementType = "GT",
        SystemEntryDate = new DateTime(2026, 1, 16, 8, 0, 0, DateTimeKind.Utc),
        PartyId = "500999999",
        SourceId = "user-1",
        MovementComments = "Entrega ao cliente",
        ShipFrom = new SaftShippingPoint
        {
            WarehouseId = "ARM1",
            Address = new SaftAddress { AddressDetail = "Rua da Fábrica", City = "Porto", PostalCode = "4000-002" }
        },
        ShipTo = new SaftShippingPoint
        {
            Address = new SaftAddress { AddressDetail = "Rua do Cliente", City = "Lisboa", PostalCode = "1000-002" }
        },
        MovementStartTime = new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc),
        MovementEndTime = new DateTime(2026, 1, 16, 18, 0, 0, DateTimeKind.Utc),
        AtDocCodeId = "ABCD1234",
        Lines =
        [
            new SaftStockMovementLine
            {
                LineNumber = 1,
                ProductCode = "ART001",
                ProductDescription = "Artigo de teste",
                Quantity = 3,
                UnitOfMeasure = "UN",
                UnitPrice = 50m,
                Description = "Artigo de teste",
                Amount = 150m,
                Tax = Tax()
            }
        ],
        Totals = new SaftDocumentTotals { TaxPayable = 34.5m, NetTotal = 150m, GrossTotal = 184.5m }
    };

    private static SaftPayment Payment() => new()
    {
        PaymentRefNo = "RG A2026/1",
        Atcud = "JFTX7RK9-1",
        Period = 1,
        TransactionDate = new DateOnly(2026, 1, 20),
        PaymentType = "RG",
        Description = "Recebimento",
        DocumentStatus = new SaftDocumentStatus
        {
            StatusDate = new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc),
            SourceId = "user-1",
            SourceBilling = "P"
        },
        PaymentMethods =
        [
            new SaftPaymentMethod { PaymentMechanism = "TB", PaymentAmount = 246m, PaymentDate = new DateOnly(2026, 1, 20) }
        ],
        SourceId = "user-1",
        SystemEntryDate = new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc),
        CustomerId = "500999999",
        Lines =
        [
            new SaftPaymentLine
            {
                LineNumber = 1,
                OriginatingOn = "FT A2026/1",
                InvoiceDate = new DateOnly(2026, 1, 15),
                Amount = 246m
            }
        ],
        Totals = new SaftDocumentTotals { TaxPayable = 0m, NetTotal = 246m, GrossTotal = 246m }
    };

    private static SaftAuditFile FullFile() => new()
    {
        Header = Header(),
        Customers = [Customer()],
        Products = [Product()],
        TaxTable = [TaxEntry()],
        Invoices = [Invoice()],
        StockMovements = [StockMovement()],
        Payments = [Payment()]
    };

    [Fact]
    public void Validate_accepts_a_file_with_all_three_document_families()
    {
        var errors = SaftSchemaValidator.Validate(SaftXmlWriter.Build(FullFile()));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_file_with_only_the_master_files()
    {
        var file = new SaftAuditFile { Header = Header(), Customers = [Customer()], Products = [Product()], TaxTable = [TaxEntry()] };

        SaftSchemaValidator.Validate(SaftXmlWriter.Build(file)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_voided_document_with_its_reason()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            Customers = [Customer()],
            Products = [Product()],
            TaxTable = [TaxEntry()],
            Invoices = [Invoice(status: "A")]
        };

        SaftSchemaValidator.Validate(SaftXmlWriter.Build(file)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_credit_note_on_the_debit_side()
    {
        var file = new SaftAuditFile
        {
            Header = Header(),
            Customers = [Customer()],
            Products = [Product()],
            TaxTable = [TaxEntry()],
            Invoices = [Invoice("NC A2026/1", "NC")]
        };

        SaftSchemaValidator.Validate(SaftXmlWriter.Build(file)).Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_the_serialized_bytes()
    {
        SaftSchemaValidator.Validate(SaftXmlWriter.Serialize(FullFile())).Should().BeEmpty();
    }

    /// <summary>Proves the validator actually rejects, so the passing tests above mean something.</summary>
    [Fact]
    public void Validate_rejects_a_bad_data_type()
    {
        XNamespace ns = SaftConstants.Namespace;

        var document = SaftXmlWriter.Build(FullFile());
        document.Root!.Element(ns + "Header")!.Element(ns + "TaxRegistrationNumber")!.Value = "not-a-number";

        SaftSchemaValidator.Validate(document).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_rejects_elements_written_out_of_order()
    {
        XNamespace ns = SaftConstants.Namespace;

        var document = SaftXmlWriter.Build(FullFile());
        var header = document.Root!.Element(ns + "Header")!;
        var companyName = header.Element(ns + "CompanyName")!;

        companyName.Remove();
        header.AddFirst(companyName);

        SaftSchemaValidator.Validate(document).Should().NotBeEmpty();
    }

    /// <summary>
    /// The published schema is XSD 1.1 and .NET is 1.0, so the assertions are dropped. Pinning the
    /// count means a future schema update that adds rules shows up here instead of passing silently.
    /// </summary>
    [Fact]
    public void SkippedAssertions_reports_the_rules_dotnet_cannot_enforce()
    {
        SaftSchemaValidator.SkippedAssertions.Should().Be(19);
    }
}
