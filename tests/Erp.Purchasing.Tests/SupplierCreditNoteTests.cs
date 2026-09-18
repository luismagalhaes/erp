using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;
using Erp.Common;

namespace Erp.Purchasing.Tests;

/// <summary>
/// Credit notes from suppliers. Recorded like any other document of theirs, with one rule of their
/// own: <b>a credit note never brings goods into stock</b>. Goods that go back leave on a return,
/// and crediting the value moves nothing.
/// </summary>
public class SupplierCreditNoteTests
{
    private readonly IPurchaseInvoiceStorage _invoices = Substitute.For<IPurchaseInvoiceStorage>();
    private readonly IGoodsReceiptStorage _receipts = Substitute.For<IGoodsReceiptStorage>();
    private readonly IStockRecorder _stock = Substitute.For<IStockRecorder>();
    private readonly ISupplierPaymentStorage _payments = Substitute.For<ISupplierPaymentStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<PurchaseInvoice> _stored = [];

    public SupplierCreditNoteTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _invoices.When(x => x.AddAsync(Arg.Any<PurchaseInvoice>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<PurchaseInvoice>()));

        _invoices.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _invoices.ExistsAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.Any(invoice =>
                invoice.Supplier.TaxId == call.ArgAt<string>(1)
                && invoice.SupplierDocumentNumber == call.ArgAt<string>(2)));

        _invoices.GetInvoicedQuantitiesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyDictionary<Guid, decimal>)new Dictionary<Guid, decimal>());
    }

    private PurchaseInvoiceService CreateService() => new(_invoices, _receipts, _stock, _payments, _unitOfWork);

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    private RecordPurchaseInvoiceRequest CreditNote(
        string number = "NC 2026/3",
        Guid? warehouseId = null,
        params PurchaseInvoiceLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            Supplier,
            PurchaseDocumentTypes.CreditNote,
            number,
            new DateOnly(2026, 3, 22),
            new DateOnly(2026, 3, 24),
            lines.Length == 0 ? [GoodsLine()] : lines,
            warehouseId);

    private static PurchaseInvoiceLineRequest GoodsLine(decimal quantity = 4m, decimal unitPrice = 5m) =>
        new("ART001", "Artigo de teste", quantity, unitPrice, "UN", "PT", "NOR", 23m, "Inventory");

    // --- A credit note never moves stock ---

    /// <summary>
    /// The goods left on the return. If the credit note brought them back in, the warehouse would
    /// end up with stock that is physically at the supplier.
    /// </summary>
    [Fact]
    public async Task A_credit_note_never_brings_goods_into_stock()
    {
        var created = await CreateService().RecordAsync(CreditNote(warehouseId: _warehouseId), "user-1");

        created.MovedStock.Should().BeFalse();

        await _stock.DidNotReceive().RecordAsync(
            Arg.Any<RecordDocumentStockRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same lines on an invoice would move stock. What changes the answer is the document type,
    /// which is exactly the distinction worth pinning down.
    /// </summary>
    [Fact]
    public async Task The_same_lines_on_an_invoice_would_move_stock()
    {
        var invoice = CreditNote(warehouseId: _warehouseId) with
        {
            DocumentType = PurchaseDocumentTypes.Invoice,
            SupplierDocumentNumber = "FT 2026/3"
        };

        var created = await CreateService().RecordAsync(invoice, "user-1");

        created.MovedStock.Should().BeTrue();
    }

    /// <summary>Nothing goes into stock, so nothing has to say where — no warehouse is required.</summary>
    [Fact]
    public async Task A_credit_note_needs_no_warehouse()
    {
        var created = await CreateService().RecordAsync(CreditNote(), "user-1");

        created.WarehouseId.Should().BeNull();
        created.MovedStock.Should().BeFalse();
    }

    // --- Otherwise it behaves like any recorded document ---

    [Fact]
    public async Task A_credit_note_carries_the_suppliers_own_number()
    {
        var created = await CreateService().RecordAsync(CreditNote("NC 2026/3"), "user-1");

        created.DocumentType.Should().Be("NC");
        created.SupplierDocumentNumber.Should().Be("NC 2026/3");
        created.GrossTotal.Should().Be(24.60m);
    }

    /// <summary>
    /// Amounts stay positive on the document, as they appear on the supplier's paper. The sign
    /// belongs to the document type, and is applied when the figures are added up.
    /// </summary>
    [Fact]
    public async Task A_credit_note_carries_positive_amounts()
    {
        var created = await CreateService().RecordAsync(CreditNote(), "user-1");

        created.NetTotal.Should().Be(20m);
        created.TaxTotal.Should().Be(4.60m);
    }

    [Fact]
    public async Task A_credit_note_can_cite_the_return_it_credits()
    {
        var returnLineId = Guid.NewGuid();
        var line = GoodsLine() with { ReturnLineId = returnLineId };

        var created = await CreateService().RecordAsync(CreditNote(lines: line), "user-1");

        created.Lines.Single().ReturnLineId.Should().Be(returnLineId);
    }

    /// <summary>The anti-duplication rule does not care which kind of document it is.</summary>
    [Fact]
    public async Task The_same_credit_note_cannot_be_recorded_twice()
    {
        var service = CreateService();
        await service.RecordAsync(CreditNote("NC 2026/3"), "user-1");

        var act = () => service.RecordAsync(CreditNote("NC 2026/3"), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already recorded*");
    }

    /// <summary>Having moved no stock, a credit note stays correctable like any bookkeeping entry.</summary>
    [Fact]
    public async Task A_credit_note_can_be_corrected()
    {
        var service = CreateService();
        var created = await service.RecordAsync(CreditNote(), "user-1");

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdatePurchaseInvoiceRequest(
                new DateOnly(2026, 3, 22),
                new DateOnly(2026, 3, 24),
                [GoodsLine(quantity: 6m)]));

        updated!.NetTotal.Should().Be(30m);
    }

    [Fact]
    public async Task Voiding_a_credit_note_reverses_no_stock()
    {
        var service = CreateService();
        var created = await service.RecordAsync(CreditNote(warehouseId: _warehouseId), "user-1");

        await service.VoidAsync(created.Id, "Engano", "user-2");

        await _stock.DidNotReceive().ReverseDocumentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
