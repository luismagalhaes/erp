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
/// Goods going back to suppliers. The mirror of a receipt: stock leaves at the cost it came in at,
/// and only what is still in hand can go.
/// </summary>
public class SupplierReturnServiceTests
{
    private readonly ISupplierReturnStorage _returns = Substitute.For<ISupplierReturnStorage>();
    private readonly IGoodsReceiptStorage _receipts = Substitute.For<IGoodsReceiptStorage>();
    private readonly IStockRecorder _stock = Substitute.For<IStockRecorder>();
    private readonly IErpUnitOfWork _unitOfWork = Substitute.For<IErpUnitOfWork>();
    private readonly IErpTransaction _transaction = Substitute.For<IErpTransaction>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<SupplierReturn> _stored = [];
    private readonly List<GoodsReceipt> _storedReceipts = [];

    public SupplierReturnServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _returns.When(x => x.AddAsync(Arg.Any<SupplierReturn>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<SupplierReturn>()));

        _returns.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _returns.GetLastSequenceAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => _stored.Count);

        _returns.NumberExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.Any(x => x.Number == call.ArgAt<string>(1)));

        // What has gone back already, derived from the stored returns the way the real one does.
        _returns.GetReturnedQuantitiesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var wanted = call.ArgAt<IReadOnlyCollection<Guid>>(0);

                return (IReadOnlyDictionary<Guid, decimal>)_stored
                    .Where(supplierReturn => !supplierReturn.IsVoided)
                    .SelectMany(supplierReturn => supplierReturn.Lines)
                    .Where(line => wanted.Contains(line.ReceiptLineId))
                    .GroupBy(line => line.ReceiptLineId)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
            });

        _receipts.GetForUpdateByLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedReceipts.FirstOrDefault(
                receipt => receipt.Lines.Any(line => line.Id == call.ArgAt<Guid>(0))));

        _receipts.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<GoodsReceipt>)[.. _storedReceipts]);
    }

    private SupplierReturnService CreateService() => new(_returns, _receipts, _stock, _unitOfWork);

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    private GoodsReceipt GivenReceipt(decimal quantity = 10m, decimal unitCost = 5m, Guid? warehouseId = null)
    {
        var receipt = GoodsReceipt.Create(
            _companyId,
            _supplierId,
            new SupplierSnapshot("F001", "Fornecedor Teste, Lda", "501234567"),
            $"REC2026/{_storedReceipts.Count + 1}",
            new DateOnly(2026, 3, 10),
            warehouseId ?? _warehouseId,
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

    private CreateSupplierReturnRequest Request(params SupplierReturnLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            Supplier,
            new DateOnly(2026, 3, 20),
            "Mercadoria danificada",
            lines);

    private static SupplierReturnLineRequest Line(GoodsReceipt receipt, decimal quantity) =>
        new(receipt.Lines.Single().Id, quantity);

    // --- Sending goods back ---

    [Fact]
    public async Task CreateAsync_records_the_return_with_its_reason()
    {
        var receipt = GivenReceipt();

        var created = await CreateService().CreateAsync(Request(Line(receipt, 4m)), "user-1");

        created.Number.Should().Be("DEV2026/1");
        created.Status.Should().Be("Returned");
        created.Reason.Should().Be("Mercadoria danificada");
        created.TotalCost.Should().Be(20m);
    }

    /// <summary>The stock leaves at the cost it came in at, not at some other price.</summary>
    [Fact]
    public async Task CreateAsync_takes_the_stock_out_at_the_cost_it_came_in_at()
    {
        var receipt = GivenReceipt(quantity: 10m, unitCost: 7.5m);

        await CreateService().CreateAsync(Request(Line(receipt, 4m)), "user-1");

        await _stock.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(request =>
                request.Direction == Erp.Inventory.Domain.StockDirection.Out
                && request.WarehouseId == _warehouseId
                && request.DocumentType == "DEV"
                && request.Lines.Count == 1
                && request.Lines[0].Quantity == 4m
                && request.Lines[0].UnitCost == 7.5m),
            "user-1",
            Arg.Any<CancellationToken>());
    }

    /// <summary>The warehouse comes from the receipt: the goods leave where they are sitting.</summary>
    [Fact]
    public async Task CreateAsync_takes_the_warehouse_from_the_receipt()
    {
        var otherWarehouse = Guid.NewGuid();
        var receipt = GivenReceipt(warehouseId: otherWarehouse);

        var created = await CreateService().CreateAsync(Request(Line(receipt, 1m)), "user-1");

        created.WarehouseId.Should().Be(otherWarehouse);
    }

    [Fact]
    public async Task CreateAsync_refuses_lines_from_more_than_one_warehouse()
    {
        var first = GivenReceipt();
        var second = GivenReceipt(warehouseId: Guid.NewGuid());

        var act = () => CreateService().CreateAsync(
            Request(Line(first, 1m), Line(second, 1m)), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*more than one warehouse*");
    }

    [Fact]
    public async Task CreateAsync_refuses_a_return_with_no_lines()
    {
        var act = () => CreateService().CreateAsync(Request(), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*sends nothing back*");
    }

    // --- Only what is still in hand ---

    [Fact]
    public async Task CreateAsync_refuses_to_send_back_more_than_was_received()
    {
        var receipt = GivenReceipt(quantity: 10m);

        var act = () => CreateService().CreateAsync(Request(Line(receipt, 11m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 10*");
    }

    [Fact]
    public async Task CreateAsync_counts_what_a_previous_return_already_sent()
    {
        var receipt = GivenReceipt(quantity: 10m);
        await CreateService().CreateAsync(Request(Line(receipt, 6m)), "user-1");

        var act = () => CreateService().CreateAsync(Request(Line(receipt, 5m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 4*");
    }

    [Fact]
    public async Task CreateAsync_counts_the_same_receipt_line_twice_in_one_return()
    {
        var receipt = GivenReceipt(quantity: 10m);

        var act = () => CreateService().CreateAsync(
            Request(Line(receipt, 6m), Line(receipt, 6m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 10*");
    }

    [Fact]
    public async Task CreateAsync_locks_the_receipt_before_reading_what_is_left()
    {
        var receipt = GivenReceipt();

        await CreateService().CreateAsync(Request(Line(receipt, 1m)), "user-1");

        await _receipts.Received(1).GetForUpdateByLineAsync(
            receipt.Lines.Single().Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_refuses_a_receipt_from_another_supplier()
    {
        var receipt = GivenReceipt();
        var request = Request(Line(receipt, 1m)) with { SupplierId = Guid.NewGuid() };

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*another supplier*");
    }

    [Fact]
    public async Task CreateAsync_commits_everything_in_one_transaction()
    {
        var receipt = GivenReceipt();

        await CreateService().CreateAsync(Request(Line(receipt, 1m)), "user-1");

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    // --- Undoing ---

    [Fact]
    public async Task VoidAsync_brings_the_stock_back_in()
    {
        var receipt = GivenReceipt();
        var created = await CreateService().CreateAsync(Request(Line(receipt, 4m)), "user-1");

        var voided = await CreateService().VoidAsync(created.Id, "Afinal ficámos com ela", "user-2");

        voided!.Status.Should().Be("Voided");

        await _stock.Received(1).ReverseDocumentAsync(
            created.Id,
            Arg.Is<string>(reason => reason.Contains("DEV2026/1")),
            "user-2",
            Arg.Any<CancellationToken>());
    }

    /// <summary>A voided return sent nothing back, so the goods are ours to send again.</summary>
    [Fact]
    public async Task Voiding_frees_the_quantity_to_be_returned_again()
    {
        var receipt = GivenReceipt(quantity: 10m);
        var created = await CreateService().CreateAsync(Request(Line(receipt, 10m)), "user-1");

        await CreateService().VoidAsync(created.Id, "Engano", "user-2");

        var again = await CreateService().CreateAsync(Request(Line(receipt, 10m)), "user-1");

        again.Lines.Single().Quantity.Should().Be(10m);
    }

    [Fact]
    public async Task VoidAsync_refuses_a_return_already_voided()
    {
        var receipt = GivenReceipt();
        var created = await CreateService().CreateAsync(Request(Line(receipt, 1m)), "user-1");
        await CreateService().VoidAsync(created.Id, "Engano", "user-2");

        var act = () => CreateService().VoidAsync(created.Id, "Outra vez", "user-2");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already voided*");
    }

    // --- What could still go back ---

    [Fact]
    public async Task GetReturnableLinesAsync_returns_what_is_still_in_hand()
    {
        var receipt = GivenReceipt(quantity: 10m);
        await CreateService().CreateAsync(Request(Line(receipt, 4m)), "user-1");

        var returnable = await CreateService().GetReturnableLinesAsync(_companyId);

        returnable.Should().ContainSingle();
        returnable[0].ReceivedQuantity.Should().Be(10m);
        returnable[0].ReturnedQuantity.Should().Be(4m);
        returnable[0].ReturnableQuantity.Should().Be(6m);
    }

    [Fact]
    public async Task GetReturnableLinesAsync_leaves_out_lines_that_all_went_back()
    {
        var receipt = GivenReceipt(quantity: 10m);
        await CreateService().CreateAsync(Request(Line(receipt, 10m)), "user-1");

        var returnable = await CreateService().GetReturnableLinesAsync(_companyId);

        returnable.Should().BeEmpty();
    }
}
