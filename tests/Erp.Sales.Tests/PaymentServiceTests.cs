using Erp.Sales.Application.Configuration;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Sales.Tests;

public class PaymentServiceTests
{
    private readonly IPaymentStorage _payments = Substitute.For<IPaymentStorage>();
    private readonly ISalesDocumentStorage _documents = Substitute.For<ISalesDocumentStorage>();
    private readonly ISeriesStorage _series = Substitute.For<ISeriesStorage>();
    private readonly ISalesUnitOfWork _unitOfWork = Substitute.For<ISalesUnitOfWork>();
    private readonly IDocumentSigner _signer = Substitute.For<IDocumentSigner>();
    private readonly ISalesTransaction _transaction = Substitute.For<ISalesTransaction>();
    private readonly List<Payment> _persisted = [];
    private readonly List<PaymentStatusChange> _persistedStatusChanges = [];
    private readonly Dictionary<Guid, decimal> _settled = [];
    private readonly List<SalesDocument> _invoices = [];
    private readonly Guid _companyId = Guid.NewGuid();

    public PaymentServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _documents.GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<SalesDocument>)[.. _invoices.Where(x => x.CompanyId == call.Arg<Guid>())]);
        _payments.GetLastHashAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(string.Empty);

        _signer.Sign(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>())
            .Returns(new DocumentSignature(new string('x', 44), "1"));

        // The settled amounts come from a dictionary the tests can pre-load, which stands in for
        // the receipts already in the database.
        _payments.GetSettledAmountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyDictionary<Guid, decimal>)call
                .Arg<IReadOnlyCollection<Guid>>()
                .Where(_settled.ContainsKey)
                .ToDictionary(id => id, id => _settled[id]));

        _payments.When(x => x.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>()))
            .Do(call => _persisted.Add(call.Arg<Payment>()));

        _payments.When(x => x.AddStatusChangeAsync(Arg.Any<PaymentStatusChange>(), Arg.Any<CancellationToken>()))
            .Do(call => _persistedStatusChanges.Add(call.Arg<PaymentStatusChange>()));
    }

    private PaymentService CreateService() =>
        new(_payments, _documents, _series, _unitOfWork, _signer,
            Options.Create(new FiscalOptions { IssuerTaxId = "123456789", CertificateNumber = "9999", KeyVersion = "1" }));

    private Series GivenSeries(string documentType = "RG", Guid? companyId = null)
    {
        var series = new Series
        {
            CompanyId = companyId ?? _companyId,
            DocumentType = documentType,
            SeriesCode = "A2026"
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _series.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        return series;
    }

    /// <summary>An issued invoice to settle, registered in the substituted document storage.</summary>
    private SalesDocument GivenInvoice(decimal grossTotal = 246m, Guid? companyId = null, string taxId = "500123456")
    {
        var invoiceSeries = new Series
        {
            CompanyId = companyId ?? _companyId,
            DocumentType = "FT",
            SeriesCode = "A2026"
        };

        invoiceSeries.Communicate("JFTX7RK9", DateTime.UtcNow);

        var sequence = invoiceSeries.TakeNextSequence();

        var invoice = SalesDocument.Issue(
            companyId ?? _companyId,
            invoiceSeries,
            sequence,
            $"FT A2026/{sequence}",
            "JFTX7RK9-1",
            new DateOnly(2026, 1, 15),
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            new CustomerSnapshot(taxId, "Cliente Teste", "Rua Um, Lisboa"),
            [],
            [],
            grossTotal,
            0m,
            grossTotal,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1");

        _documents.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _invoices.Add(invoice);

        return invoice;
    }

    private CreatePaymentRequest Request(Guid seriesId, params CreatePaymentLineRequest[] lines) =>
        new(_companyId,
            seriesId,
            new DateOnly(2026, 2, 20),
            "500123456",
            "Cliente Teste",
            lines,
            [new CreatePaymentMethodRequest("TB", lines.Sum(line => line.AppliedAmount), new DateOnly(2026, 2, 20))]);

    [Fact]
    public async Task IssueAsync_numbers_the_receipt_from_the_series()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var issued = await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        issued.PaymentRefNo.Should().Be("RG A2026/1");
        issued.Atcud.Should().Be("JFTX7RK9-1");
        issued.GrossTotal.Should().Be(246m);
        series.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_records_the_settled_invoice_on_the_line()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var issued = await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 100m)), "user-1");

        var line = issued.Lines.Should().ContainSingle().Subject;
        line.OriginatingDocumentId.Should().Be(invoice.Id);
        line.OriginatingNumber.Should().Be(invoice.DocumentNumber);
        line.OriginatingDate.Should().Be(invoice.DocumentDate);
        line.AppliedAmount.Should().Be(100m);
    }

    [Fact]
    public async Task IssueAsync_leaves_the_tax_at_zero_outside_the_cash_vat_regime()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var issued = await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        // The VAT was already accounted for on the invoice; the receipt only moves money.
        issued.TaxPayable.Should().Be(0m);
        issued.NetTotal.Should().Be(issued.GrossTotal);
    }

    [Fact]
    public async Task IssueAsync_chains_the_signature_onto_the_previous_receipt()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();
        _payments.GetLastHashAsync(series.Id, Arg.Any<CancellationToken>()).Returns("previous-hash");

        await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        _signer.Received(1).Sign(
            new DateOnly(2026, 2, 20),
            Arg.Is<DateTime>(date => date.Kind == DateTimeKind.Utc),
            "RG A2026/1",
            246m,
            "previous-hash");

        _persisted.Should().ContainSingle().Which.PreviousHash.Should().Be("previous-hash");
    }

    [Fact]
    public async Task IssueAsync_rejects_more_than_the_invoice_owes()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice(grossTotal: 246m);

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 300m)), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*owes*was applied to it*");
    }

    [Fact]
    public async Task IssueAsync_takes_earlier_receipts_into_account()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice(grossTotal: 246m);
        _settled[invoice.Id] = 200m;

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 50m)), "user-1");

        // Only 46 of the 246 are still owed, so 50 is too much.
        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*owes {46m:0.00}*");
    }

    [Fact]
    public async Task IssueAsync_rejects_payment_methods_that_do_not_add_up()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var request = new CreatePaymentRequest(
            _companyId,
            series.Id,
            new DateOnly(2026, 2, 20),
            "500123456",
            "Cliente Teste",
            [new CreatePaymentLineRequest(invoice.Id, 246m)],
            [new CreatePaymentMethodRequest("NU", 100m, new DateOnly(2026, 2, 20))]);

        var act = () => CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*add up to {100m:0.00}*");
    }

    [Fact]
    public async Task IssueAsync_rejects_an_unknown_payment_mechanism()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var request = new CreatePaymentRequest(
            _companyId,
            series.Id,
            new DateOnly(2026, 2, 20),
            "500123456",
            "Cliente Teste",
            [new CreatePaymentLineRequest(invoice.Id, 246m)],
            [new CreatePaymentMethodRequest("ZZ", 246m, new DateOnly(2026, 2, 20))]);

        var act = () => CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ZZ*");
    }

    [Fact]
    public async Task IssueAsync_rejects_a_series_that_is_not_for_receipts()
    {
        var series = GivenSeries(documentType: "FT");
        var invoice = GivenInvoice();

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not for receipts*");
    }

    [Fact]
    public async Task IssueAsync_rejects_a_series_of_another_company()
    {
        var series = GivenSeries(companyId: Guid.NewGuid());
        var invoice = GivenInvoice();

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task IssueAsync_rejects_a_series_without_a_validation_code()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "RG", SeriesCode = "A2026" };
        _series.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);
        var invoice = GivenInvoice();

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*validation code*");
    }

    [Fact]
    public async Task IssueAsync_rejects_the_same_invoice_twice_on_one_receipt()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var act = () => CreateService().IssueAsync(
            Request(series.Id,
                new CreatePaymentLineRequest(invoice.Id, 100m),
                new CreatePaymentLineRequest(invoice.Id, 100m)),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot be settled twice*");
    }

    [Fact]
    public async Task IssueAsync_rejects_a_voided_invoice()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();
        invoice.Void("Erro de faturação", "user-1", DateTime.UtcNow);

        var act = () => CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*is voided*");
    }

    [Fact]
    public async Task IssueAsync_commits_the_transaction_that_holds_the_series_lock()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        await _series.Received(1).GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOutstandingInvoicesAsync_subtracts_what_is_already_settled()
    {
        GivenSeries();
        var invoice = GivenInvoice(grossTotal: 246m);
        _settled[invoice.Id] = 46m;

        var outstanding = await CreateService().GetOutstandingInvoicesAsync(_companyId);

        var result = outstanding.Should().ContainSingle().Subject;
        result.GrossTotal.Should().Be(246m);
        result.SettledAmount.Should().Be(46m);
        result.OutstandingAmount.Should().Be(200m);
    }

    [Fact]
    public async Task GetOutstandingInvoicesAsync_leaves_out_fully_settled_invoices()
    {
        var invoice = GivenInvoice(grossTotal: 246m);
        _settled[invoice.Id] = 246m;

        var outstanding = await CreateService().GetOutstandingInvoicesAsync(_companyId);

        outstanding.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOutstandingInvoicesAsync_filters_by_customer()
    {
        GivenInvoice(taxId: "500123456");
        GivenInvoice(taxId: "500999999");

        var outstanding = await CreateService().GetOutstandingInvoicesAsync(_companyId, "500999999");

        outstanding.Should().ContainSingle().Which.CustomerTaxId.Should().Be("500999999");
    }

    [Fact]
    public async Task VoidAsync_records_a_status_change_without_altering_the_receipt()
    {
        var series = GivenSeries();
        var invoice = GivenInvoice();

        var issued = await CreateService().IssueAsync(
            Request(series.Id, new CreatePaymentLineRequest(invoice.Id, 246m)), "user-1");

        var payment = _persisted.Should().ContainSingle().Subject;
        _payments.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(payment);

        var voided = await CreateService().VoidAsync(issued.Id, "Recebimento anulado", "user-2");

        voided!.Status.Should().Be("A");
        payment.GrossTotal.Should().Be(246m);
        payment.Hash.Should().Be(new string('x', 44));

        var change = _persistedStatusChanges.Should().ContainSingle().Subject;
        change.Reason.Should().Be("Recebimento anulado");
        change.UserId.Should().Be("user-2");
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_an_unknown_receipt()
    {
        var result = await CreateService().VoidAsync(Guid.NewGuid(), "Motivo", "user-1");

        result.Should().BeNull();
    }
}
