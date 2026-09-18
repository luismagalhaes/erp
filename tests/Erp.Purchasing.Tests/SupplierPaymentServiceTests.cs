using Erp.Common;
using Erp.Inventory.Infrastructure.Application;
using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Purchasing.Tests;

/// <summary>
/// Paying suppliers. The same rules as the receipts on the sales side: never more than a document
/// still owes, never a voided document, and the payment methods add up to what is settled.
/// </summary>
public class SupplierPaymentServiceTests
{
    private readonly ISupplierPaymentStorage _payments = Substitute.For<ISupplierPaymentStorage>();
    private readonly IPurchaseInvoiceStorage _invoices = Substitute.For<IPurchaseInvoiceStorage>();
    private readonly ISelfBilledInvoiceStorage _selfBilled = Substitute.For<ISelfBilledInvoiceStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();
    private readonly FakeDocumentNumbers _numbers = new();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    private readonly List<PurchaseInvoice> _storedInvoices = [];
    private readonly List<SupplierPayment> _storedPayments = [];

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    public SupplierPaymentServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _invoices.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedInvoices.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _invoices.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<PurchaseInvoice>)[.. _storedInvoices]);

        _selfBilled.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SelfBilledInvoice>)[]);

        _payments.When(x => x.AddAsync(Arg.Any<SupplierPayment>(), Arg.Any<CancellationToken>()))
            .Do(call => _storedPayments.Add(call.Arg<SupplierPayment>()));

        _payments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedPayments.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        // What is already paid, derived from the stored payments the way the real one does.
        _payments.GetPaidAmountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var wanted = call.ArgAt<IReadOnlyCollection<Guid>>(0);

                return (IReadOnlyDictionary<Guid, decimal>)_storedPayments
                    .Where(payment => !payment.IsVoided)
                    .SelectMany(payment => payment.Lines)
                    .Where(line => wanted.Contains(line.DocumentId))
                    .GroupBy(line => line.DocumentId)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.AppliedAmount));
            });
    }

    private SupplierPaymentService CreateService() =>
        new(_payments, _invoices, _selfBilled, _numbers, _unitOfWork);

    /// <summary>Records a services invoice of 100 + 23% VAT, so 123.00 owed.</summary>
    private PurchaseInvoice GivenInvoice(
        string documentType = PurchaseDocumentTypes.Invoice,
        Guid? supplierId = null,
        string number = "FT 2026/17")
    {
        var invoice = PurchaseInvoice.Create(
            _companyId,
            supplierId ?? _supplierId,
            new SupplierSnapshot("F001", "Fornecedor Teste, Lda", "501234567"),
            documentType,
            number,
            new DateOnly(2026, 3, 12),
            new DateOnly(2026, 3, 14),
            [new PurchaseInvoiceLine
            {
                ProductCode = "SRV",
                ProductDescription = "Transporte",
                Quantity = 1m,
                UnitPrice = 100m,
                TaxPercentage = 23m,
                DeductionNature = DeductionNature.OtherGoodsAndServices
            }],
            dueDate: new DateOnly(2026, 4, 12));

        _storedInvoices.Add(invoice);
        return invoice;
    }

    private CreateSupplierPaymentRequest Request(
        IReadOnlyList<SupplierPaymentLineRequest> lines,
        decimal? methodAmount = null) =>
        new(_companyId,
            _supplierId,
            Supplier,
            new DateOnly(2026, 4, 10),
            lines,
            [new SupplierPaymentMethodRequest("TB", methodAmount ?? NetOf(lines), new DateOnly(2026, 4, 10))]);

    /// <summary>What the lines send once the credit notes among them are taken off.</summary>
    private decimal NetOf(IReadOnlyList<SupplierPaymentLineRequest> lines) =>
        lines.Sum(line => _storedInvoices.Any(invoice => invoice.Id == line.DocumentId && invoice.IsCredit)
            ? -line.AppliedAmount
            : line.AppliedAmount);

    private static SupplierPaymentLineRequest Settle(PurchaseInvoice invoice, decimal amount) =>
        new(nameof(PayableDocumentKind.PurchaseInvoice), invoice.Id, amount);

    // --- Recording ---

    [Fact]
    public async Task RecordAsync_settles_the_invoice_and_takes_our_next_number()
    {
        var invoice = GivenInvoice();

        var result = await CreateService().RecordAsync(Request([Settle(invoice, 123m)]), "user-1");

        result.Number.Should().Be("PAG2026/1");
        result.Total.Should().Be(123m);
        result.IsVoided.Should().BeFalse();
        result.Supplier.TaxId.Should().Be("501234567");
        result.Lines.Should().ContainSingle();
        result.Lines[0].DocumentNumber.Should().Be("FT 2026/17");
        result.Lines[0].DocumentKind.Should().Be("PurchaseInvoice");
        result.Methods.Should().ContainSingle().Which.Mechanism.Should().Be("TB");

        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_accepts_a_partial_payment_and_then_the_rest()
    {
        var invoice = GivenInvoice();
        var service = CreateService();

        await service.RecordAsync(Request([Settle(invoice, 23m)]));
        var second = await service.RecordAsync(Request([Settle(invoice, 100m)]));

        second.Number.Should().Be("PAG2026/2");
        (await service.GetPayableDocumentsAsync(_companyId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_refuses_to_pay_more_than_is_owed()
    {
        var invoice = GivenInvoice();
        var service = CreateService();
        await service.RecordAsync(Request([Settle(invoice, 100m)]));

        var act = () => service.RecordAsync(Request([Settle(invoice, 24m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*owes 23*");
    }

    [Fact]
    public async Task RecordAsync_refuses_a_voided_invoice()
    {
        var invoice = GivenInvoice();
        invoice.Void("Engano", "user-1", DateTime.UtcNow);

        var act = () => CreateService().RecordAsync(Request([Settle(invoice, 123m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*voided*");
    }

    [Fact]
    public async Task RecordAsync_refuses_an_invoice_receipt_which_came_already_paid()
    {
        var invoice = GivenInvoice(PurchaseDocumentTypes.InvoiceReceipt);

        var act = () => CreateService().RecordAsync(Request([Settle(invoice, 10m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*leaves nothing owed*");
    }

    // --- Credit notes ---

    /// <summary>A supplier credit note of 123.00, the same figures as <see cref="GivenInvoice"/>.</summary>
    private PurchaseInvoice GivenCreditNote(string number = "NC 2026/3") =>
        GivenInvoice(PurchaseDocumentTypes.CreditNote, number: number);

    [Fact]
    public async Task RecordAsync_takes_the_credit_note_off_what_is_paid()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote();

        var result = await CreateService().RecordAsync(
            Request([Settle(invoice, 123m), Settle(creditNote, 23m)]));

        result.Total.Should().Be(100m);
        result.Lines.Should().HaveCount(2);
        result.Lines.Single(line => line.IsCredit).DocumentNumber.Should().Be("NC 2026/3");
        result.Methods.Should().ContainSingle().Which.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task RecordAsync_refuses_methods_that_ignore_the_credit()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote();

        var act = () => CreateService().RecordAsync(
            Request([Settle(invoice, 123m), Settle(creditNote, 23m)], methodAmount: 123m));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*add up to 123*");
    }

    [Fact]
    public async Task RecordAsync_settles_by_credit_alone_with_no_money_moving()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote();

        var request = Request([Settle(invoice, 123m), Settle(creditNote, 123m)]) with { Methods = [] };
        var result = await CreateService().RecordAsync(request);

        result.Total.Should().Be(0m);
        result.Methods.Should().BeEmpty();
        (await CreateService().GetPayableDocumentsAsync(_companyId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_refuses_more_credit_than_the_invoices_settled()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote();

        var act = () => CreateService().RecordAsync(
            Request([Settle(invoice, 50m), Settle(creditNote, 60m)]) with { Methods = [] });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exceed the documents settled*");
    }

    [Fact]
    public async Task RecordAsync_refuses_a_credit_note_on_its_own()
    {
        var creditNote = GivenCreditNote();

        var act = () => CreateService().RecordAsync(Request([Settle(creditNote, 10m)]) with { Methods = [] });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*at least one document*");
    }

    [Fact]
    public async Task RecordAsync_refuses_to_use_more_credit_than_is_left()
    {
        var creditNote = GivenCreditNote();
        var service = CreateService();
        await service.RecordAsync(Request([Settle(GivenInvoice(), 123m), Settle(creditNote, 100m)]));

        var act = () => service.RecordAsync(
            Request([Settle(GivenInvoice(number: "FT 2026/20"), 123m), Settle(creditNote, 24m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*credit left*");
    }

    [Fact]
    public async Task Voiding_the_payment_gives_the_credit_back()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote();
        var service = CreateService();
        var payment = await service.RecordAsync(Request([Settle(invoice, 123m), Settle(creditNote, 123m)]) with { Methods = [] });

        await service.VoidAsync(payment.Id, "Engano");

        var open = await service.GetPayableDocumentsAsync(_companyId);
        open.Should().HaveCount(2);
        open.Single(document => document.IsCredit).OutstandingAmount.Should().Be(123m);
    }

    [Fact]
    public async Task RecordAsync_refuses_a_document_of_another_supplier()
    {
        var invoice = GivenInvoice(supplierId: Guid.NewGuid());

        var act = () => CreateService().RecordAsync(Request([Settle(invoice, 123m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*another supplier*");
    }

    [Fact]
    public async Task RecordAsync_refuses_the_same_document_twice()
    {
        var invoice = GivenInvoice();

        var act = () => CreateService().RecordAsync(Request([Settle(invoice, 20m), Settle(invoice, 20m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*twice*");
    }

    [Fact]
    public async Task RecordAsync_refuses_methods_that_do_not_add_up()
    {
        var invoice = GivenInvoice();

        var act = () => CreateService().RecordAsync(Request([Settle(invoice, 123m)], methodAmount: 120m));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*add up to 120*");
        _storedPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_refuses_an_unknown_payment_mechanism()
    {
        var invoice = GivenInvoice();
        var request = Request([Settle(invoice, 123m)]) with
        {
            Methods = [new SupplierPaymentMethodRequest("XX", 123m, new DateOnly(2026, 4, 10))]
        };

        var act = () => CreateService().RecordAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*mechanism*");
    }

    [Fact]
    public async Task RecordAsync_refuses_an_unknown_document_kind()
    {
        var invoice = GivenInvoice();

        var act = () => CreateService().RecordAsync(
            Request([new SupplierPaymentLineRequest("Order", invoice.Id, 10m)]));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*document kind*");
    }

    // --- What is owed ---

    [Fact]
    public async Task GetPayableDocumentsAsync_returns_what_is_still_owed()
    {
        var invoice = GivenInvoice();
        var creditNote = GivenCreditNote("NC 2026/1");
        GivenInvoice(PurchaseDocumentTypes.InvoiceReceipt, number: "FR 2026/1");
        GivenInvoice(number: "FT 2026/18").Void("Engano", "user-1", DateTime.UtcNow);
        GivenCreditNote("NC 2026/2").Void("Engano", "user-1", DateTime.UtcNow);

        var service = CreateService();
        await service.RecordAsync(Request([Settle(invoice, 23m)]));

        var payable = await service.GetPayableDocumentsAsync(_companyId);

        // Invoices first, then the credit available to take off them.
        payable.Should().HaveCount(2);
        payable[0].DocumentId.Should().Be(invoice.Id);
        payable[0].IsCredit.Should().BeFalse();
        payable[0].GrossTotal.Should().Be(123m);
        payable[0].PaidAmount.Should().Be(23m);
        payable[0].OutstandingAmount.Should().Be(100m);
        payable[0].DueDate.Should().Be(new DateOnly(2026, 4, 12));
        payable[1].DocumentId.Should().Be(creditNote.Id);
        payable[1].IsCredit.Should().BeTrue();
        payable[1].OutstandingAmount.Should().Be(123m);
    }

    // --- Voiding ---

    [Fact]
    public async Task VoidAsync_puts_the_invoice_back_into_what_is_owed()
    {
        var invoice = GivenInvoice();
        var service = CreateService();
        var payment = await service.RecordAsync(Request([Settle(invoice, 123m)]));

        var voided = await service.VoidAsync(payment.Id, "Transferência devolvida", "user-2");

        voided!.IsVoided.Should().BeTrue();
        voided.VoidReason.Should().Be("Transferência devolvida");
        (await service.GetPayableDocumentsAsync(_companyId))
            .Should().ContainSingle().Which.OutstandingAmount.Should().Be(123m);
    }

    [Fact]
    public async Task VoidAsync_refuses_to_void_twice()
    {
        var invoice = GivenInvoice();
        var service = CreateService();
        var payment = await service.RecordAsync(Request([Settle(invoice, 123m)]));
        await service.VoidAsync(payment.Id, "Engano");

        var act = () => service.VoidAsync(payment.Id, "Outra vez");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_a_payment_that_does_not_exist()
    {
        var result = await CreateService().VoidAsync(Guid.NewGuid(), "Engano");

        result.Should().BeNull();
    }

    // --- The invoice side of the same rule ---

    [Fact]
    public async Task A_paid_invoice_cannot_be_voided()
    {
        var invoice = GivenInvoice();
        await CreateService().RecordAsync(Request([Settle(invoice, 10m)]));

        var invoiceService = new PurchaseInvoiceService(
            _invoices,
            Substitute.For<IGoodsReceiptStorage>(),
            Substitute.For<IStockRecorder>(),
            _payments,
            _unitOfWork);

        var act = () => invoiceService.VoidAsync(invoice.Id, "Engano");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*payments against it*");
        invoice.IsVoided.Should().BeFalse();
    }
}
