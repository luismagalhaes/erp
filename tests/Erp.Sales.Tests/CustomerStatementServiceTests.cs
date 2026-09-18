using Erp.Common;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.SeriesRegistry.Domain;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

/// <summary>
/// The customer's current account statement: what they were charged, what they were given back and
/// what they paid, with the running balance of what they owe.
/// </summary>
public class CustomerStatementServiceTests
{
    private const string TaxId = "500123456";

    private readonly ISalesDocumentStorage _documents = Substitute.For<ISalesDocumentStorage>();
    private readonly IPaymentStorage _payments = Substitute.For<IPaymentStorage>();
    private readonly List<SalesDocument> _storedDocuments = [];
    private readonly List<Payment> _storedReceipts = [];
    private readonly Guid _companyId = Guid.NewGuid();
    private int _sequence;

    public CustomerStatementServiceTests()
    {
        _documents.GetForCustomerAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<SalesDocument>)[.. _storedDocuments]);

        _payments.GetForPartyAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<Payment>)[.. _storedReceipts]);
    }

    private CustomerStatementService CreateService() => new(_documents, _payments);

    private static Series CommunicatedSeries(Guid companyId, string documentType)
    {
        var series = new Series { CompanyId = companyId, DocumentType = documentType, SeriesCode = "A2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        return series;
    }

    private SalesDocument GivenDocument(string documentType, decimal grossTotal, DateOnly date, SalesDocument? rectifies = null)
    {
        var sequence = ++_sequence;

        var document = SalesDocument.Issue(
            _companyId,
            CommunicatedSeries(_companyId, documentType),
            sequence,
            $"{documentType} A2026/{sequence}",
            $"JFTX7RK9-{sequence}",
            date,
            date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
            new CustomerSnapshot(TaxId, "Cliente Teste", "Rua Um"),
            [],
            [],
            grossTotal,
            0m,
            grossTotal,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1",
            rectifies is null ? null : new RectifiedDocument(rectifies.Id, rectifies.DocumentNumber, "Devolução"));

        _storedDocuments.Add(document);
        return document;
    }

    private Payment GivenReceipt(decimal amount, DateOnly date)
    {
        var sequence = ++_sequence;

        var receipt = Payment.Issue(
            _companyId,
            CommunicatedSeries(_companyId, "RG"),
            sequence,
            $"RG A2026/{sequence}",
            $"JFTX7RK9-{sequence}",
            date,
            date.ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc),
            new PaymentParty(TaxId, "Cliente Teste"),
            null,
            [],
            [],
            amount,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1");

        _storedReceipts.Add(receipt);
        return receipt;
    }

    [Fact]
    public async Task Invoices_are_debits_credit_notes_and_receipts_are_credits()
    {
        var invoice = GivenDocument("FT", 246m, new DateOnly(2026, 1, 10));
        GivenDocument("NC", 46m, new DateOnly(2026, 1, 12), rectifies: invoice);
        GivenReceipt(150m, new DateOnly(2026, 1, 20));

        var statement = await CreateService().GetAsync(_companyId, TaxId, "Cliente Teste");

        statement.OpeningBalance.Should().Be(0m);
        statement.Entries.Select(entry => (entry.Debit, entry.Credit, entry.Balance)).Should().Equal(
            (246m, 0m, 246m),
            (0m, 46m, 200m),
            (0m, 150m, 50m));
        statement.TotalDebit.Should().Be(246m);
        statement.TotalCredit.Should().Be(196m);
        statement.ClosingBalance.Should().Be(50m);
        statement.Entries[1].Description.Should().Be(invoice.DocumentNumber);
        statement.Entries[2].Source.Should().Be(Constants.StatementSources.Receipt);
    }

    [Fact]
    public async Task An_invoice_receipt_charges_and_pays_on_the_same_line()
    {
        GivenDocument("FR", 100m, new DateOnly(2026, 1, 10));

        var statement = await CreateService().GetAsync(_companyId, TaxId, "Cliente Teste");

        statement.Entries.Should().ContainSingle().Which.Should().Match<Erp.Common.Statements.AccountStatementEntry>(
            entry => entry.Debit == 100m && entry.Credit == 100m && entry.Balance == 0m);
    }

    [Fact]
    public async Task What_came_before_the_period_is_the_opening_balance()
    {
        GivenDocument("FT", 100m, new DateOnly(2025, 12, 5));
        GivenReceipt(40m, new DateOnly(2025, 12, 20));
        GivenDocument("FT", 50m, new DateOnly(2026, 1, 5));
        GivenDocument("FT", 999m, new DateOnly(2026, 2, 5));

        var statement = await CreateService().GetAsync(
            _companyId, TaxId, "Cliente Teste", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        statement.OpeningBalance.Should().Be(60m);
        statement.Entries.Should().ContainSingle().Which.Balance.Should().Be(110m);
        statement.ClosingBalance.Should().Be(110m);
    }

    [Fact]
    public async Task Voided_documents_and_receipts_are_left_out()
    {
        GivenDocument("FT", 100m, new DateOnly(2026, 1, 5));
        GivenDocument("FT", 70m, new DateOnly(2026, 1, 6)).Void("Engano", "user-1", DateTime.UtcNow);
        GivenReceipt(100m, new DateOnly(2026, 1, 7)).Void("Engano", "user-1", DateTime.UtcNow);

        var statement = await CreateService().GetAsync(_companyId, TaxId, "Cliente Teste");

        statement.Entries.Should().ContainSingle();
        statement.ClosingBalance.Should().Be(100m);
    }

    [Fact]
    public async Task On_the_same_day_the_invoice_comes_before_the_receipt_that_pays_it()
    {
        var day = new DateOnly(2026, 1, 5);
        GivenReceipt(100m, day);
        GivenDocument("FT", 100m, day);

        var statement = await CreateService().GetAsync(_companyId, TaxId, "Cliente Teste");

        statement.Entries.Select(entry => entry.Balance).Should().Equal(100m, 0m);
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_is_refused()
    {
        var act = () => CreateService().GetAsync(
            _companyId, TaxId, "Cliente Teste", new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
