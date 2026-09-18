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
/// Recording supplier invoices. Two rules carry the weight: the same document may only be recorded
/// once, and goods a receipt already brought in are not brought in again.
/// </summary>
public class PurchaseInvoiceServiceTests
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
    private readonly List<GoodsReceipt> _storedReceipts = [];

    public PurchaseInvoiceServiceTests()
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

        // What is already invoiced, derived from the stored invoices the way the real one does.
        _invoices.GetInvoicedQuantitiesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var wanted = call.ArgAt<IReadOnlyCollection<Guid>>(0);
                var exclude = call.ArgAt<Guid?>(1);

                return (IReadOnlyDictionary<Guid, decimal>)_stored
                    .Where(invoice => !invoice.IsVoided && invoice.Id != exclude)
                    .SelectMany(invoice => invoice.Lines)
                    .Where(line => line.ReceiptLineId is not null && wanted.Contains(line.ReceiptLineId.Value))
                    .GroupBy(line => line.ReceiptLineId!.Value)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
            });

        _receipts.GetForUpdateByLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedReceipts.FirstOrDefault(
                receipt => receipt.Lines.Any(line => line.Id == call.ArgAt<Guid>(0))));

        _receipts.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<GoodsReceipt>)[.. _storedReceipts]);
    }

    private PurchaseInvoiceService CreateService() => new(_invoices, _receipts, _stock, _payments, _unitOfWork);

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    /// <summary>Records a receipt for the invoice to come against.</summary>
    private GoodsReceipt GivenReceipt(decimal quantity = 10m, decimal unitCost = 5m)
    {
        var receipt = GoodsReceipt.Create(
            _companyId,
            _supplierId,
            new SupplierSnapshot("F001", "Fornecedor Teste, Lda", "501234567"),
            $"REC2026/{_storedReceipts.Count + 1}",
            new DateOnly(2026, 3, 10),
            _warehouseId,
            [new GoodsReceiptLine
            {
                ProductCode = "ART001",
                ProductDescription = "Artigo de teste",
                Quantity = quantity,
                UnitCost = unitCost
            }]);

        _storedReceipts.Add(receipt);
        return receipt;
    }

    private RecordPurchaseInvoiceRequest Request(
        string number = "FT 2026/17",
        Guid? warehouseId = null,
        params PurchaseInvoiceLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            Supplier,
            PurchaseDocumentTypes.Invoice,
            number,
            new DateOnly(2026, 3, 12),
            new DateOnly(2026, 3, 14),
            lines.Length == 0 ? [GoodsLine()] : lines,
            warehouseId);

    private static PurchaseInvoiceLineRequest GoodsLine(
        decimal quantity = 10m,
        decimal unitPrice = 5m,
        Guid? receiptLineId = null) =>
        new("ART001", "Artigo de teste", quantity, unitPrice, "UN", "PT", "NOR", 23m, "Inventory", receiptLineId);

    private static PurchaseInvoiceLineRequest ServiceLine(decimal amount = 100m) =>
        new("SRV", "Transporte", 1m, amount, "UN", "PT", "NOR", 23m, "OtherGoodsAndServices");

    // --- Recording ---

    [Fact]
    public async Task RecordAsync_stores_the_suppliers_own_number_and_date()
    {
        var created = await CreateService().RecordAsync(Request(warehouseId: _warehouseId), "user-1");

        created.SupplierDocumentNumber.Should().Be("FT 2026/17");
        created.SupplierDocumentDate.Should().Be(new DateOnly(2026, 3, 12));
        created.ReceivedDate.Should().Be(new DateOnly(2026, 3, 14));
        created.Status.Should().Be("Recorded");
    }

    [Fact]
    public async Task RecordAsync_computes_the_totals_and_the_tax_breakdown()
    {
        var created = await CreateService().RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine(10m, 5m), ServiceLine(100m)]), "user-1");

        created.NetTotal.Should().Be(150m);
        created.TaxTotal.Should().Be(34.50m);
        created.GrossTotal.Should().Be(184.50m);

        // Both lines are at 23%, so they collapse into one line of the VAT return.
        created.TaxSummary.Should().ContainSingle();
        created.TaxSummary[0].TaxableBase.Should().Be(150m);
        created.TaxSummary[0].TaxAmount.Should().Be(34.50m);
    }

    [Fact]
    public async Task RecordAsync_splits_the_tax_breakdown_by_rate()
    {
        var reduced = new PurchaseInvoiceLineRequest(
            "ART002", "Artigo reduzido", 10m, 10m, "UN", "PT", "RED", 6m, "Inventory");

        var created = await CreateService().RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine(10m, 5m), reduced]), "user-1");

        created.TaxSummary.Should().HaveCount(2);
        created.TaxSummary.Sum(summary => summary.TaxAmount).Should().Be(17.50m);
    }

    [Fact]
    public async Task RecordAsync_refuses_a_document_dated_after_it_reached_us()
    {
        var request = Request(warehouseId: _warehouseId) with { ReceivedDate = new DateOnly(2026, 3, 1) };

        var act = () => CreateService().RecordAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*before it was issued*");
    }

    [Fact]
    public async Task RecordAsync_refuses_an_unknown_document_type()
    {
        var request = Request(warehouseId: _warehouseId) with { DocumentType = "XX" };

        var act = () => CreateService().RecordAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Unknown supplier document type*");
    }

    // --- Double recording ---

    /// <summary>
    /// The real fiscal risk of this module: the same invoice arrives on paper and again by email,
    /// gets recorded twice, and the VAT is deducted twice without anything looking wrong.
    /// </summary>
    [Fact]
    public async Task RecordAsync_refuses_the_same_document_from_the_same_supplier_twice()
    {
        var service = CreateService();
        await service.RecordAsync(Request("FT 2026/17", _warehouseId), "user-1");

        var act = () => service.RecordAsync(Request("FT 2026/17", _warehouseId), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already recorded*");
    }

    /// <summary>Two suppliers both issue their FT 2026/1, and both are perfectly valid.</summary>
    [Fact]
    public async Task RecordAsync_accepts_the_same_number_from_a_different_supplier()
    {
        var service = CreateService();
        await service.RecordAsync(Request("FT 2026/1", _warehouseId), "user-1");

        var other = Request("FT 2026/1", _warehouseId) with
        {
            SupplierId = Guid.NewGuid(),
            Supplier = new PurchaseOrderSupplierDto("F002", "Outro Fornecedor", "502222222")
        };

        var created = await service.RecordAsync(other, "user-1");

        created.SupplierDocumentNumber.Should().Be("FT 2026/1");
    }

    // --- The integrating-document rule ---

    /// <summary>
    /// The goods came in on the receipt. The invoice that follows must not bring them in again —
    /// the same rule that stops an invoice from re-moving what a delivery note moved in Sales.
    /// </summary>
    [Fact]
    public async Task RecordAsync_does_not_move_stock_for_lines_that_came_from_a_receipt()
    {
        var receipt = GivenReceipt(quantity: 10m);
        var receiptLineId = receipt.Lines.Single().Id;

        var created = await CreateService().RecordAsync(
            Request(lines: [GoodsLine(10m, 5m, receiptLineId)]), "user-1");

        created.MovedStock.Should().BeFalse();
        created.Lines.Single().MovesStock.Should().BeFalse();

        // Nothing is recorded at all: with no warehouse there is nothing for the ledger to do.
        await _stock.DidNotReceive().RecordAsync(
            Arg.Any<RecordDocumentStockRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The invoice that travelled with the lorry: no receipt, so it brings the goods in.</summary>
    [Fact]
    public async Task RecordAsync_moves_stock_when_no_receipt_brought_the_goods_in()
    {
        var created = await CreateService().RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine(7m, 5m)]), "user-1");

        created.MovedStock.Should().BeTrue();

        await _stock.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(request =>
                request.Direction == Erp.Inventory.Domain.StockDirection.In
                && request.Lines.Count == 1
                && request.Lines[0].Quantity == 7m
                && request.Lines[0].UnitCost == 5m),
            "user-1",
            Arg.Any<CancellationToken>());
    }

    /// <summary>Services never move stock, whatever else the invoice carries.</summary>
    [Fact]
    public async Task RecordAsync_leaves_service_lines_out_of_the_ledger()
    {
        await CreateService().RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine(3m), ServiceLine()]), "user-1");

        await _stock.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(request => request.Lines.Count == 1),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_refuses_goods_with_nowhere_to_put_them()
    {
        var act = () => CreateService().RecordAsync(Request(lines: [GoodsLine()]), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*needs a warehouse*");
    }

    /// <summary>A services invoice has no goods, so it needs no warehouse.</summary>
    [Fact]
    public async Task RecordAsync_needs_no_warehouse_for_services()
    {
        var created = await CreateService().RecordAsync(Request(lines: [ServiceLine()]), "user-1");

        created.WarehouseId.Should().BeNull();
        created.MovedStock.Should().BeFalse();
    }

    // --- Against the receipt ---

    [Fact]
    public async Task RecordAsync_refuses_to_invoice_more_than_was_received()
    {
        var receipt = GivenReceipt(quantity: 10m);

        var act = () => CreateService().RecordAsync(
            Request(lines: [GoodsLine(11m, 5m, receipt.Lines.Single().Id)]), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 10*");
    }

    [Fact]
    public async Task RecordAsync_counts_what_a_previous_invoice_already_took()
    {
        var receipt = GivenReceipt(quantity: 10m);
        var receiptLineId = receipt.Lines.Single().Id;

        var service = CreateService();
        await service.RecordAsync(Request("FT 2026/1", lines: [GoodsLine(6m, 5m, receiptLineId)]), "user-1");

        var act = () => service.RecordAsync(
            Request("FT 2026/2", lines: [GoodsLine(5m, 5m, receiptLineId)]), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 4*");
    }

    [Fact]
    public async Task RecordAsync_locks_the_receipt_before_reading_what_is_left()
    {
        var receipt = GivenReceipt();

        await CreateService().RecordAsync(
            Request(lines: [GoodsLine(1m, 5m, receipt.Lines.Single().Id)]), "user-1");

        await _receipts.Received(1).GetForUpdateByLineAsync(
            receipt.Lines.Single().Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_refuses_a_receipt_from_another_supplier()
    {
        var receipt = GivenReceipt();
        var request = Request(lines: [GoodsLine(1m, 5m, receipt.Lines.Single().Id)]) with
        {
            SupplierId = Guid.NewGuid()
        };

        var act = () => CreateService().RecordAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*another supplier*");
    }

    // --- Correcting and striking out ---

    /// <summary>
    /// Bookkeeping is free to be corrected: nothing here was issued by us, so there is no
    /// rectifying document to raise.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_corrects_a_record_that_moved_no_stock()
    {
        var service = CreateService();
        var created = await service.RecordAsync(Request(lines: [ServiceLine(100m)]), "user-1");

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdatePurchaseInvoiceRequest(
                new DateOnly(2026, 3, 12),
                new DateOnly(2026, 3, 14),
                [ServiceLine(250m)]));

        updated!.NetTotal.Should().Be(250m);
    }

    /// <summary>
    /// Once it brought goods in, rewriting it would leave the ledger saying one thing and the
    /// invoice another.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_refuses_a_record_that_brought_goods_into_stock()
    {
        var service = CreateService();
        var created = await service.RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine()]), "user-1");

        var act = () => service.UpdateAsync(
            created.Id,
            new UpdatePurchaseInvoiceRequest(
                new DateOnly(2026, 3, 12), new DateOnly(2026, 3, 14), [ServiceLine()]));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*brought goods into stock*");
    }

    [Fact]
    public async Task VoidAsync_takes_back_the_stock_it_brought_in()
    {
        var service = CreateService();
        var created = await service.RecordAsync(
            Request(warehouseId: _warehouseId, lines: [GoodsLine()]), "user-1");

        var voided = await service.VoidAsync(created.Id, "Registei-a por engano", "user-2");

        voided!.Status.Should().Be("Voided");

        await _stock.Received(1).ReverseDocumentAsync(
            created.Id, Arg.Any<string>(), "user-2", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Goods a receipt brought in stay in the warehouse: that receipt is still standing, and
    /// undoing it is its own decision.
    /// </summary>
    [Fact]
    public async Task VoidAsync_leaves_alone_the_stock_a_receipt_brought_in()
    {
        var receipt = GivenReceipt();
        var service = CreateService();
        var created = await service.RecordAsync(
            Request(lines: [GoodsLine(10m, 5m, receipt.Lines.Single().Id)]), "user-1");

        await service.VoidAsync(created.Id, "Registei-a por engano", "user-2");

        await _stock.DidNotReceive().ReverseDocumentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A voided record invoices nothing, so what it took goes back on the shelf.</summary>
    [Fact]
    public async Task Voiding_frees_the_receipt_quantity_for_another_invoice()
    {
        var receipt = GivenReceipt(quantity: 10m);
        var receiptLineId = receipt.Lines.Single().Id;

        var service = CreateService();
        var created = await service.RecordAsync(
            Request("FT 2026/1", lines: [GoodsLine(10m, 5m, receiptLineId)]), "user-1");

        await service.VoidAsync(created.Id, "Engano", "user-2");

        var again = await service.RecordAsync(
            Request("FT 2026/2", lines: [GoodsLine(10m, 5m, receiptLineId)]), "user-1");

        again.Lines.Single().Quantity.Should().Be(10m);
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_a_record_that_does_not_exist()
    {
        var result = await CreateService().VoidAsync(Guid.NewGuid(), "Engano", "user-2");

        result.Should().BeNull();
    }

    // --- What is still to be invoiced ---

    [Fact]
    public async Task GetUninvoicedReceiptLinesAsync_returns_what_is_received_but_not_invoiced()
    {
        var receipt = GivenReceipt(quantity: 10m);
        await CreateService().RecordAsync(
            Request(lines: [GoodsLine(4m, 5m, receipt.Lines.Single().Id)]), "user-1");

        var pending = await CreateService().GetUninvoicedReceiptLinesAsync(_companyId);

        pending.Should().ContainSingle();
        pending[0].ReceivedQuantity.Should().Be(10m);
        pending[0].InvoicedQuantity.Should().Be(4m);
        pending[0].PendingQuantity.Should().Be(6m);
    }

    [Fact]
    public async Task GetUninvoicedReceiptLinesAsync_leaves_out_fully_invoiced_lines()
    {
        var receipt = GivenReceipt(quantity: 10m);
        await CreateService().RecordAsync(
            Request(lines: [GoodsLine(10m, 5m, receipt.Lines.Single().Id)]), "user-1");

        var pending = await CreateService().GetUninvoicedReceiptLinesAsync(_companyId);

        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_commits_everything_in_one_transaction()
    {
        await CreateService().RecordAsync(Request(warehouseId: _warehouseId), "user-1");

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
