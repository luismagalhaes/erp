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
/// Receiving goods. This is where a purchase meets stock, so most of what matters here is that the
/// receipt, the ledger and the order end up agreeing about what arrived.
/// </summary>
public class GoodsReceiptServiceTests
{
    private readonly IGoodsReceiptStorage _receipts = Substitute.For<IGoodsReceiptStorage>();
    private readonly IPurchaseOrderStorage _orders = Substitute.For<IPurchaseOrderStorage>();
    private readonly IStockRecorder _stock = Substitute.For<IStockRecorder>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<GoodsReceipt> _stored = [];
    private readonly List<PurchaseOrder> _placedOrders = [];

    public GoodsReceiptServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _receipts.When(x => x.AddAsync(Arg.Any<GoodsReceipt>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<GoodsReceipt>()));

        _receipts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _orders.GetForUpdateByLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _placedOrders.FirstOrDefault(
                order => order.Lines.Any(line => line.Id == call.ArgAt<Guid>(0))));

        _orders.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _placedOrders.FirstOrDefault(order => order.Id == call.ArgAt<Guid>(0)));
    }

    private GoodsReceiptService CreateService() => new(_receipts, _orders, _stock, new FakeDocumentNumbers(), _unitOfWork);

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    /// <summary>Places an order for the receipt to come against.</summary>
    private PurchaseOrder GivenPlacedOrder(decimal quantity = 10m, decimal unitPrice = 5m)
    {
        var order = PurchaseOrder.Create(
            _companyId,
            _supplierId,
            new SupplierSnapshot("F001", "Fornecedor Teste, Lda", "501234567"),
            $"ENC2026/{_placedOrders.Count + 1}",
            new DateOnly(2026, 3, 1),
            _warehouseId,
            [new PurchaseOrderLine
            {
                ProductCode = "ART001",
                ProductDescription = "Artigo de teste",
                Quantity = quantity,
                UnitPrice = unitPrice,
                TaxPercentage = 23m
            }]);

        order.Place();
        _placedOrders.Add(order);

        return order;
    }

    private CreateGoodsReceiptRequest Request(params GoodsReceiptLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            Supplier,
            new DateOnly(2026, 3, 10),
            _warehouseId,
            lines.Length == 0 ? [FreeLine()] : lines,
            "GR 2026/455",
            new DateOnly(2026, 3, 9));

    /// <summary>A line with no order behind it — goods that simply turned up.</summary>
    private static GoodsReceiptLineRequest FreeLine(decimal quantity = 10m, decimal unitCost = 5m) =>
        new("ART001", "Artigo de teste", quantity, unitCost);

    private static GoodsReceiptLineRequest OrderedLine(
        PurchaseOrder order,
        decimal quantity,
        decimal unitCost = 5m) =>
        new("ART001", "Artigo de teste", quantity, unitCost, "UN", order.Lines.Single().Id);

    // --- Recording what arrived ---

    [Fact]
    public async Task CreateAsync_stores_the_receipt_with_the_supplier_document()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");

        created.Number.Should().Be("REC2026/1");
        created.Status.Should().Be("Received");
        created.SupplierDocumentNumber.Should().Be("GR 2026/455");
        created.TotalCost.Should().Be(50m);
    }

    [Fact]
    public async Task CreateAsync_refuses_a_receipt_with_no_lines()
    {
        var request = Request() with { Lines = [] };

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*received nothing*");
    }

    // --- Stock ---

    /// <summary>The whole point of the receipt: the goods come into the warehouse.</summary>
    [Fact]
    public async Task CreateAsync_brings_the_stock_in()
    {
        await CreateService().CreateAsync(Request(FreeLine(quantity: 7m)), "user-1");

        await _stock.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(request =>
                request.Direction == Erp.Inventory.Domain.StockDirection.In
                && request.WarehouseId == _warehouseId
                && request.DocumentType == "REC"
                && request.Lines.Count == 1
                && request.Lines[0].Quantity == 7m),
            "user-1",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Purchases are the only place a real cost enters the ledger, so the cost has to travel with
    /// the movement — without it, weighted-average costing has nothing to work from.
    /// </summary>
    [Fact]
    public async Task CreateAsync_carries_the_cost_into_the_ledger()
    {
        await CreateService().CreateAsync(Request(FreeLine(quantity: 4m, unitCost: 12.5m)), "user-1");

        await _stock.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(request => request.Lines[0].UnitCost == 12.5m),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The receipt, the order's quantities and the ledger have to land together, or the warehouse
    /// and the order end up disagreeing about what arrived.
    /// </summary>
    [Fact]
    public async Task CreateAsync_commits_everything_in_one_transaction()
    {
        await CreateService().CreateAsync(Request(), "user-1");

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    // --- Against the order ---

    [Fact]
    public async Task CreateAsync_credits_the_order_line_it_came_against()
    {
        var order = GivenPlacedOrder(quantity: 10m);

        await CreateService().CreateAsync(Request(OrderedLine(order, 4m)), "user-1");

        order.Lines.Single().ReceivedQuantity.Should().Be(4m);
        order.Lines.Single().PendingQuantity.Should().Be(6m);
        order.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
    }

    [Fact]
    public async Task CreateAsync_closes_the_order_when_everything_arrived()
    {
        var order = GivenPlacedOrder(quantity: 10m);

        await CreateService().CreateAsync(Request(OrderedLine(order, 10m)), "user-1");

        order.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    /// <summary>
    /// The three-way match: what is received cannot exceed what is still owed. The same rule, and
    /// the same locking, as invoicing from a delivery note on the sales side.
    /// </summary>
    [Fact]
    public async Task CreateAsync_refuses_to_receive_more_than_the_order_still_owes()
    {
        var order = GivenPlacedOrder(quantity: 10m);

        var act = () => CreateService().CreateAsync(Request(OrderedLine(order, 11m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 10*");
    }

    [Fact]
    public async Task CreateAsync_counts_what_a_previous_receipt_already_took()
    {
        var order = GivenPlacedOrder(quantity: 10m);
        await CreateService().CreateAsync(Request(OrderedLine(order, 6m)), "user-1");

        var act = () => CreateService().CreateAsync(Request(OrderedLine(order, 5m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 4*");
    }

    /// <summary>The same order line twice in one receipt has to add up against the same ceiling.</summary>
    [Fact]
    public async Task CreateAsync_counts_the_same_order_line_twice_in_one_receipt()
    {
        var order = GivenPlacedOrder(quantity: 10m);

        var act = () => CreateService().CreateAsync(
            Request(OrderedLine(order, 6m), OrderedLine(order, 6m)), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only 10*");
    }

    /// <summary>
    /// The order is locked before its pending quantity is read. Without that, two receipts arriving
    /// at once would each see the same room and both pass.
    /// </summary>
    [Fact]
    public async Task CreateAsync_locks_the_order_before_reading_what_is_left()
    {
        var order = GivenPlacedOrder();

        await CreateService().CreateAsync(Request(OrderedLine(order, 1m)), "user-1");

        await _orders.Received(1).GetForUpdateByLineAsync(
            order.Lines.Single().Id, Arg.Any<CancellationToken>());
    }

    /// <summary>Goods that turn up without an order are received anyway; refusing helps nobody.</summary>
    [Fact]
    public async Task CreateAsync_accepts_goods_that_came_without_an_order()
    {
        var created = await CreateService().CreateAsync(Request(FreeLine()), "user-1");

        created.Lines.Should().ContainSingle().Which.OrderLineId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_refuses_an_order_line_from_another_supplier()
    {
        var order = GivenPlacedOrder();
        var request = Request(OrderedLine(order, 1m)) with { SupplierId = Guid.NewGuid() };

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*another supplier*");
    }

    [Fact]
    public async Task CreateAsync_refuses_an_order_line_that_does_not_exist()
    {
        var request = Request(new GoodsReceiptLineRequest("ART001", "Artigo", 1m, 5m, "UN", Guid.NewGuid()));

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*was not found*");
    }

    // --- Undoing ---

    [Fact]
    public async Task VoidAsync_takes_the_stock_back_out()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");

        var voided = await CreateService().VoidAsync(created.Id, "Devolvido ao fornecedor", "user-2");

        voided!.Status.Should().Be("Voided");
        voided.VoidReason.Should().Be("Devolvido ao fornecedor");

        await _stock.Received(1).ReverseDocumentAsync(
            created.Id,
            Arg.Is<string>(reason => reason.Contains("REC2026/1") && reason.Contains("Devolvido")),
            "user-2",
            Arg.Any<CancellationToken>());
    }

    /// <summary>Undoing a receipt puts the quantity back on the order, so what is owed is right again.</summary>
    [Fact]
    public async Task VoidAsync_gives_the_order_its_quantity_back()
    {
        var order = GivenPlacedOrder(quantity: 10m);
        var created = await CreateService().CreateAsync(Request(OrderedLine(order, 10m)), "user-1");
        order.Status.Should().Be(PurchaseOrderStatus.Received);

        await CreateService().VoidAsync(created.Id, "Enganei-me", "user-2");

        order.Lines.Single().ReceivedQuantity.Should().Be(0m);
        order.Lines.Single().PendingQuantity.Should().Be(10m);
        order.Status.Should().Be(PurchaseOrderStatus.Placed);
    }

    [Fact]
    public async Task VoidAsync_refuses_a_receipt_already_voided()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");
        await CreateService().VoidAsync(created.Id, "Enganei-me", "user-2");

        var act = () => CreateService().VoidAsync(created.Id, "Outra vez", "user-2");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already voided*");
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_a_receipt_that_does_not_exist()
    {
        var result = await CreateService().VoidAsync(Guid.NewGuid(), "Enganei-me", "user-2");

        result.Should().BeNull();
    }

    [Fact]
    public async Task VoidAsync_commits_everything_in_one_transaction()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");
        _transaction.ClearReceivedCalls();
        _unitOfWork.ClearReceivedCalls();

        await CreateService().VoidAsync(created.Id, "Enganei-me", "user-2");

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
