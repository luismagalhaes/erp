using Erp.Common;
using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Purchasing.Tests;

/// <summary>
/// The supplier's current account statement: what they charged us, what they gave back and what we
/// paid, with the running balance of what we owe.
/// </summary>
public class SupplierStatementServiceTests
{
    private readonly IPurchaseInvoiceStorage _invoices = Substitute.For<IPurchaseInvoiceStorage>();
    private readonly ISelfBilledInvoiceStorage _selfBilled = Substitute.For<ISelfBilledInvoiceStorage>();
    private readonly ISupplierPaymentStorage _payments = Substitute.For<ISupplierPaymentStorage>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly List<PurchaseInvoice> _storedInvoices = [];
    private readonly List<SupplierPayment> _storedPayments = [];

    private static readonly SupplierSnapshot Supplier = new("F001", "Fornecedor Teste, Lda", "501234567");

    public SupplierStatementServiceTests()
    {
        _invoices.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<PurchaseInvoice>)[.. _storedInvoices]);

        _selfBilled.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SelfBilledInvoice>)[]);

        _payments.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<SupplierPayment>)[.. _storedPayments]);
    }

    private SupplierStatementService CreateService() => new(_invoices, _selfBilled, _payments);

    /// <summary>A services document of <paramref name="net"/> + 23% VAT.</summary>
    private PurchaseInvoice GivenDocument(string documentType, decimal net, DateOnly date, string number)
    {
        var invoice = PurchaseInvoice.Create(
            _companyId,
            _supplierId,
            Supplier,
            documentType,
            number,
            date,
            date,
            [new PurchaseInvoiceLine
            {
                ProductCode = "SRV",
                ProductDescription = "Transporte",
                Quantity = 1m,
                UnitPrice = net,
                TaxPercentage = 23m,
                DeductionNature = DeductionNature.OtherGoodsAndServices
            }],
            dueDate: date.AddDays(30));

        _storedInvoices.Add(invoice);
        return invoice;
    }

    private SupplierPayment GivenPayment(DateOnly date, decimal money, params (PurchaseInvoice Document, decimal Amount)[] settled)
    {
        var payment = SupplierPayment.Create(
            _companyId,
            _supplierId,
            Supplier,
            $"PAG2026/{_storedPayments.Count + 1}",
            date,
            [.. settled.Select(entry => new SupplierPaymentLine
            {
                DocumentKind = PayableDocumentKind.PurchaseInvoice,
                DocumentId = entry.Document.Id,
                DocumentType = entry.Document.DocumentType,
                DocumentNumber = entry.Document.SupplierDocumentNumber,
                DocumentDate = entry.Document.SupplierDocumentDate,
                AppliedAmount = entry.Amount
            })],
            money == 0m ? [] : [new SupplierPaymentMethod { Mechanism = "TB", Amount = money, PaymentDate = date }]);

        _storedPayments.Add(payment);
        return payment;
    }

    private Task<Erp.Common.Statements.AccountStatement> StatementAsync(DateOnly? start = null, DateOnly? end = null) =>
        CreateService().GetAsync(_companyId, _supplierId, "Fornecedor Teste, Lda", "501234567", start, end);

    [Fact]
    public async Task Invoices_are_credits_credit_notes_and_payments_are_debits()
    {
        var invoice = GivenDocument("FT", 100m, new DateOnly(2026, 3, 1), "FT 1");
        GivenDocument("NC", 20m, new DateOnly(2026, 3, 5), "NC 1");
        GivenPayment(new DateOnly(2026, 3, 10), 50m, (invoice, 50m));

        var statement = await StatementAsync();

        statement.Entries.Select(entry => (entry.Debit, entry.Credit, entry.Balance)).Should().Equal(
            (0m, 123m, 123m),
            (24.6m, 0m, 98.4m),
            (50m, 0m, 48.4m));
        statement.ClosingBalance.Should().Be(48.4m);
        statement.Entries[0].DueDate.Should().Be(new DateOnly(2026, 3, 31));
        statement.Entries[2].Source.Should().Be(Constants.StatementSources.SupplierPayment);
    }

    /// <summary>
    /// The credit note lowered the balance when it was recorded. The payment that uses its credit
    /// moves the account only by the money that left, or the credit would count twice.
    /// </summary>
    [Fact]
    public async Task A_credit_note_used_in_a_payment_is_not_taken_off_twice()
    {
        var invoice = GivenDocument("FT", 100m, new DateOnly(2026, 3, 1), "FT 1");
        var creditNote = GivenDocument("NC", 20m, new DateOnly(2026, 3, 5), "NC 1");
        GivenPayment(new DateOnly(2026, 3, 10), 98.4m, (invoice, 123m), (creditNote, 24.6m));

        var statement = await StatementAsync();

        statement.Entries[2].Debit.Should().Be(98.4m);
        statement.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task A_payment_made_entirely_of_credit_moves_nothing()
    {
        var invoice = GivenDocument("FT", 20m, new DateOnly(2026, 3, 1), "FT 1");
        var creditNote = GivenDocument("NC", 20m, new DateOnly(2026, 3, 5), "NC 1");
        GivenPayment(new DateOnly(2026, 3, 10), 0m, (invoice, 24.6m), (creditNote, 24.6m));

        var statement = await StatementAsync();

        statement.Entries.Should().HaveCount(3);
        statement.Entries[2].Debit.Should().Be(0m);
        statement.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task What_came_before_the_period_is_the_opening_balance()
    {
        var old = GivenDocument("FT", 100m, new DateOnly(2026, 1, 10), "FT 1");
        GivenPayment(new DateOnly(2026, 1, 20), 23m, (old, 23m));
        GivenDocument("FT", 10m, new DateOnly(2026, 2, 10), "FT 2");

        var statement = await StatementAsync(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        statement.OpeningBalance.Should().Be(100m);
        statement.Entries.Should().ContainSingle().Which.Balance.Should().Be(112.3m);
    }

    [Fact]
    public async Task Voided_documents_and_payments_are_left_out()
    {
        var invoice = GivenDocument("FT", 100m, new DateOnly(2026, 3, 1), "FT 1");
        GivenDocument("FT", 50m, new DateOnly(2026, 3, 2), "FT 2").Void("Engano", "user-1", DateTime.UtcNow);
        GivenPayment(new DateOnly(2026, 3, 3), 123m, (invoice, 123m)).Void("Devolvida", "user-1", DateTime.UtcNow);

        var statement = await StatementAsync();

        statement.Entries.Should().ContainSingle();
        statement.ClosingBalance.Should().Be(123m);
    }

    [Fact]
    public async Task An_invoice_receipt_leaves_the_balance_where_it_was()
    {
        GivenDocument("FR", 100m, new DateOnly(2026, 3, 1), "FR 1");

        var statement = await StatementAsync();

        statement.Entries.Should().ContainSingle();
        statement.Entries[0].Debit.Should().Be(123m);
        statement.Entries[0].Credit.Should().Be(123m);
        statement.ClosingBalance.Should().Be(0m);
    }
}
