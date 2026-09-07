using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Purchasing.Tests;

public class PurchaseOrderServiceTests
{
    private readonly IPurchaseOrderStorage _storage = Substitute.For<IPurchaseOrderStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<PurchaseOrder> _orders = [];

    public PurchaseOrderServiceTests()
    {
        _storage.When(x => x.AddAsync(Arg.Any<PurchaseOrder>(), Arg.Any<CancellationToken>()))
            .Do(call => _orders.Add(call.Arg<PurchaseOrder>()));

        _storage.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _orders.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _storage.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<PurchaseOrder>)[.. _orders]);

        _storage.GetPendingAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<PurchaseOrder>)[.. _orders.Where(order => order.IsOpen)]);

        _storage.NumberExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _orders.Any(order => order.Number == call.ArgAt<string>(1)));

        _storage.GetLastSequenceAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => _orders.Count);
    }

    private PurchaseOrderService CreateService() => new(_storage);

    private static PurchaseOrderLineRequest Line(
        string productCode = "ART001",
        decimal quantity = 10m,
        decimal unitPrice = 5m) =>
        new(productCode, "Artigo de teste", quantity, unitPrice);

    private CreatePurchaseOrderRequest Request(bool place = false, params PurchaseOrderLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            new PurchaseOrderSupplierDto("F001", "Fornecedor Teste, Lda", "501234567"),
            new DateOnly(2026, 3, 1),
            _warehouseId,
            lines.Length == 0 ? [Line()] : lines,
            new DateOnly(2026, 3, 15),
            Place: place);

    [Fact]
    public async Task CreateAsync_stores_the_order_as_a_draft()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");

        created.Status.Should().Be("Draft");
        created.Lines.Should().ContainSingle();
        await _storage.Received(1).AddAsync(Arg.Any<PurchaseOrder>(), Arg.Any<CancellationToken>());
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_can_place_the_order_straight_away()
    {
        var created = await CreateService().CreateAsync(Request(place: true), "user-1");

        created.Status.Should().Be("Placed");
    }

    /// <summary>The number is ours and means nothing to anyone else, but it still has to be unique.</summary>
    [Fact]
    public async Task CreateAsync_numbers_orders_in_sequence_for_the_year()
    {
        var service = CreateService();

        var first = await service.CreateAsync(Request(), "user-1");
        var second = await service.CreateAsync(Request(), "user-1");

        first.Number.Should().Be("ENC2026/1");
        second.Number.Should().Be("ENC2026/2");
    }

    /// <summary>
    /// A number already taken is skipped rather than allowed to collide. A gap costs nothing here,
    /// unlike a fiscal series where it would have to be explained.
    /// </summary>
    [Fact]
    public async Task CreateAsync_skips_a_number_that_is_already_taken()
    {
        _storage.GetLastSequenceAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
        _storage.NumberExistsAsync(_companyId, "ENC2026/1", Arg.Any<CancellationToken>()).Returns(true);

        var created = await CreateService().CreateAsync(Request(), "user-1");

        created.Number.Should().Be("ENC2026/2");
    }

    [Fact]
    public async Task CreateAsync_copies_the_supplier_onto_the_order()
    {
        var created = await CreateService().CreateAsync(Request(), "user-1");

        created.Supplier.Name.Should().Be("Fornecedor Teste, Lda");
        created.Supplier.TaxId.Should().Be("501234567");
    }

    [Fact]
    public async Task CreateAsync_refuses_an_order_with_no_supplier()
    {
        var request = Request() with { SupplierId = Guid.Empty };

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*who it is being placed with*");
    }

    /// <summary>Goods cannot be expected before they were ordered.</summary>
    [Fact]
    public async Task CreateAsync_refuses_an_expected_date_before_the_order_date()
    {
        var request = Request() with { ExpectedDate = new DateOnly(2026, 2, 1) };

        var act = () => CreateService().CreateAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*before the order date*");
    }

    [Fact]
    public async Task UpdateAsync_rewrites_the_lines()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(), "user-1");

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdatePurchaseOrderRequest(
                new DateOnly(2026, 3, 1),
                _warehouseId,
                [Line("ART002", 3m, 20m)],
                new DateOnly(2026, 3, 20)));

        updated!.Lines.Should().ContainSingle().Which.ProductCode.Should().Be("ART002");
        updated.NetTotal.Should().Be(60m);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_order_that_does_not_exist()
    {
        var result = await CreateService().UpdateAsync(
            Guid.NewGuid(),
            new UpdatePurchaseOrderRequest(new DateOnly(2026, 3, 1), _warehouseId, [Line()]));

        result.Should().BeNull();
    }

    [Fact]
    public async Task PlaceAsync_sends_a_draft_to_the_supplier()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(), "user-1");

        var placed = await service.PlaceAsync(created.Id);

        placed!.Status.Should().Be("Placed");
    }

    [Fact]
    public async Task CloseAsync_records_the_reason()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(place: true), "user-1");

        var closed = await service.CloseAsync(created.Id, new ClosePurchaseOrderRequest("Fornecedor fechou"));

        closed!.Status.Should().Be("Closed");
        closed.ClosedReason.Should().Be("Fornecedor fechou");
    }

    [Fact]
    public async Task CancelAsync_calls_off_an_order_that_received_nothing()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(place: true), "user-1");

        var cancelled = await service.CancelAsync(created.Id, new ClosePurchaseOrderRequest("Já não é preciso"));

        cancelled!.Status.Should().Be("Cancelled");
    }

    // --- What is still owed ---

    [Fact]
    public async Task GetPendingLinesAsync_returns_what_the_supplier_still_owes()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(place: true, Line(quantity: 10m)), "user-1");
        _orders.Single().RegisterReceipt(_orders.Single().Lines.Single().Id, 4m);

        var pending = await service.GetPendingLinesAsync(_companyId);

        pending.Should().ContainSingle();
        pending[0].OrderNumber.Should().Be(created.Number);
        pending[0].PendingQuantity.Should().Be(6m);
        pending[0].ReceivedQuantity.Should().Be(4m);
        pending[0].WarehouseId.Should().Be(_warehouseId);
    }

    [Fact]
    public async Task GetPendingLinesAsync_leaves_out_lines_that_are_fully_received()
    {
        var service = CreateService();
        await service.CreateAsync(Request(place: true, Line("ART001", 10m), Line("ART002", 5m)), "user-1");

        var order = _orders.Single();
        order.RegisterReceipt(order.Lines.First(line => line.ProductCode == "ART001").Id, 10m);

        var pending = await service.GetPendingLinesAsync(_companyId);

        pending.Should().ContainSingle().Which.ProductCode.Should().Be("ART002");
    }

    [Fact]
    public async Task GetPendingLinesAsync_ignores_orders_that_are_not_open()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request(place: true), "user-1");
        await service.CancelAsync(created.Id, new ClosePurchaseOrderRequest("Já não é preciso"));

        var pending = await service.GetPendingLinesAsync(_companyId);

        pending.Should().BeEmpty();
    }
}
