using Erp.Inventory.Application.Services;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;
using Erp.Common;

namespace Erp.Inventory.Tests;

/// <summary>
/// Opening and closing counts. The interesting part is the closing: the adjustments have to land on
/// the balance as it stands then, inside one transaction.
/// </summary>
public class InventoryCountServiceTests
{
    private readonly IInventoryCountStorage _countStorage = Substitute.For<IInventoryCountStorage>();
    private readonly IStockStorage _stockStorage = Substitute.For<IStockStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<StockLedgerEntry> _entries = [];
    private readonly List<StockBalance> _balances = [];
    private readonly List<InventoryCount> _counts = [];

    public InventoryCountServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _countStorage.When(x => x.AddAsync(Arg.Any<InventoryCount>(), Arg.Any<CancellationToken>()))
            .Do(call => _counts.Add(call.Arg<InventoryCount>()));

        _countStorage.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _counts.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _countStorage.HasOpenCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => _counts.Any(x => x.IsOpen));

        _stockStorage.GetBalancesAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<StockBalance>)[.. _balances]);

        _stockStorage.GetBalanceForUpdateAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _balances.FirstOrDefault(x => x.ProductCode == call.ArgAt<string>(2)));

        _stockStorage.When(x => x.AddEntryAsync(Arg.Any<StockLedgerEntry>(), Arg.Any<CancellationToken>()))
            .Do(call => _entries.Add(call.Arg<StockLedgerEntry>()));

        _stockStorage.When(x => x.AddBalanceAsync(Arg.Any<StockBalance>(), Arg.Any<CancellationToken>()))
            .Do(call => _balances.Add(call.Arg<StockBalance>()));
    }

    private InventoryCountService CreateService() => new(_countStorage, _stockStorage, _unitOfWork);

    /// <summary>Puts a product in the warehouse, the way an issued document would have.</summary>
    private StockBalance GivenStock(string productCode = "ART001", decimal quantity = 10m, decimal? unitCost = null)
    {
        var balance = StockBalance.Start(_companyId, _warehouseId, productCode, "Artigo de teste");
        balance.Apply(StockLedgerEntry.FromAdjustment(
            _companyId, _warehouseId, productCode, "Artigo de teste", quantity,
            new DateOnly(2026, 1, 1), "Entrada inicial", Guid.NewGuid(), "user-1", unitCost));

        _balances.Add(balance);
        return balance;
    }

    private OpenInventoryCountRequest Request(bool startAtZero = false, params string[] productCodes) =>
        new(_companyId,
            "INV2026-01",
            new DateOnly(2026, 12, 31),
            _warehouseId,
            productCodes.Length == 0 ? null : productCodes,
            startAtZero);

    [Fact]
    public async Task OpenAsync_takes_a_line_per_product_in_scope()
    {
        GivenStock();
        GivenStock("ART002", 4m);

        var count = await CreateService().OpenAsync(Request(), "user-1");

        count.LineCount.Should().Be(2);
        count.Status.Should().Be("Open");
    }

    /// <summary>A partial count narrows the scope to the products named.</summary>
    [Fact]
    public async Task OpenAsync_narrows_the_count_to_the_products_asked_for()
    {
        GivenStock();
        GivenStock("ART002", 4m);

        var count = await CreateService().OpenAsync(Request(productCodes: "ART002"), "user-1");

        count.Lines.Should().ContainSingle().Which.ProductCode.Should().Be("ART002");
        count.Scope.Should().Be("Products");
    }

    /// <summary>
    /// Two counts open at once would each close against balances the other moved, so the second one
    /// is refused rather than allowed to fight the first.
    /// </summary>
    [Fact]
    public async Task OpenAsync_refuses_a_second_open_count()
    {
        GivenStock();
        await CreateService().OpenAsync(Request(), "user-1");

        var act = () => CreateService().OpenAsync(Request(), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already an open count*");
    }

    /// <summary>
    /// A company that starts using the system with a full warehouse has no balances at all, and a
    /// count is how that stock gets in. Refusing to open an empty sheet locked the only door from
    /// the inside — so an empty scope opens, and products are added as they are found.
    /// </summary>
    [Fact]
    public async Task OpenAsync_opens_an_empty_sheet_when_the_system_holds_nothing()
    {
        var count = await CreateService().OpenAsync(Request(), "user-1");

        count.LineCount.Should().Be(0);
        count.Status.Should().Be("Open");
    }

    /// <summary>
    /// Named products go on the sheet whether or not the system has heard of them: the caller asked
    /// for them, and leaving them out quietly is how a count misses what it was opened to find.
    /// </summary>
    [Fact]
    public async Task OpenAsync_puts_a_named_product_on_the_sheet_even_with_no_balance()
    {
        GivenStock("ART001", 10m);

        var count = await CreateService().OpenAsync(Request(false, "ART001", "ART999"), "user-1");

        count.LineCount.Should().Be(2);
        count.Lines.Single(line => line.ProductCode == "ART999").SystemQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task AddLineAsync_puts_a_product_on_an_open_sheet_at_zero()
    {
        var opened = await CreateService().OpenAsync(Request(), "user-1");

        var count = await CreateService().AddLineAsync(
            opened.Id, new AddCountLineRequest(_warehouseId, "ART001", "Artigo de teste"));

        var line = count!.Lines.Should().ContainSingle().Subject;
        line.ProductCode.Should().Be("ART001");
        line.SystemQuantity.Should().Be(0m);
        line.CountedQuantity.Should().Be(0m);
    }

    /// <summary>
    /// The opening-stock case end to end: nothing in the system, a sheet opened empty, a product
    /// added with what it cost, and the ledger left holding both the quantity and the value.
    /// </summary>
    [Fact]
    public async Task A_count_can_carry_opening_stock_and_its_cost_into_an_empty_warehouse()
    {
        var service = CreateService();
        var opened = await service.OpenAsync(Request(), "user-1");

        var count = await service.AddLineAsync(
            opened.Id, new AddCountLineRequest(_warehouseId, "ART001", "Artigo de teste", UnitCost: 7m));

        await service.SetCountedAsync(
            opened.Id, [new CountedLineRequest(count!.Lines[0].Id, 20m)]);

        await service.CloseAsync(opened.Id, "user-1");

        var entry = _entries.Should().ContainSingle().Subject;
        entry.Quantity.Should().Be(20m);
        entry.UnitCost.Should().Be(7m);

        var balance = _balances.Should().ContainSingle().Subject;
        balance.Quantity.Should().Be(20m);
        balance.AverageCost.Should().Be(7m);
        balance.StockValue.Should().Be(140m);
    }

    /// <summary>
    /// An ordinary count finds goods, not prices. What it turns up with no cost is worth the average
    /// of what was already there.
    /// </summary>
    [Fact]
    public async Task A_line_added_without_a_cost_takes_the_average_already_on_the_product()
    {
        GivenStock("ART001", 10m, unitCost: 4m);

        var service = CreateService();
        var opened = await service.OpenAsync(Request(), "user-1");

        await service.SetCountedAsync(opened.Id, [new CountedLineRequest(opened.Lines[0].Id, 15m)]);
        await service.CloseAsync(opened.Id, "user-1");

        // The five found are worth what the ten already there were worth, and the ledger says so.
        _entries.Should().ContainSingle().Which.UnitCost.Should().Be(4m);

        var balance = _balances.Should().ContainSingle().Subject;
        balance.Quantity.Should().Be(15m);
        balance.AverageCost.Should().Be(4m);
        balance.StockValue.Should().Be(60m);
    }

    [Fact]
    public async Task AddLineAsync_refuses_a_product_already_on_the_sheet()
    {
        GivenStock("ART001", 10m);
        var opened = await CreateService().OpenAsync(Request(), "user-1");

        var act = () => CreateService().AddLineAsync(
            opened.Id, new AddCountLineRequest(_warehouseId, "ART001", "Artigo de teste"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already on this count sheet*");
    }

    /// <summary>A count of one warehouse that reached into another would close outside its scope.</summary>
    [Fact]
    public async Task AddLineAsync_refuses_a_line_for_another_warehouse()
    {
        var opened = await CreateService().OpenAsync(Request(), "user-1");

        var act = () => CreateService().AddLineAsync(
            opened.Id, new AddCountLineRequest(Guid.NewGuid(), "ART001", "Artigo de teste"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*count is of one warehouse*");
    }

    [Fact]
    public async Task AddLineAsync_returns_null_for_a_count_that_does_not_exist()
    {
        var result = await CreateService().AddLineAsync(
            Guid.NewGuid(), new AddCountLineRequest(_warehouseId, "ART001", "Artigo"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetCountedAsync_records_what_was_found()
    {
        GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");
        var lineId = opened.Lines.Single().Id;

        var result = await CreateService().SetCountedAsync(opened.Id, [new CountedLineRequest(lineId, 7m)]);

        result!.Lines.Single().CountedQuantity.Should().Be(7m);
        result.LinesWithDifference.Should().Be(1);
    }

    [Fact]
    public async Task CloseAsync_writes_an_adjustment_for_the_difference()
    {
        GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");
        var lineId = opened.Lines.Single().Id;
        await CreateService().SetCountedAsync(opened.Id, [new CountedLineRequest(lineId, 7m)]);

        var closed = await CreateService().CloseAsync(opened.Id, "user-2");

        closed!.Status.Should().Be("Closed");
        _entries.Should().ContainSingle().Which.Quantity.Should().Be(-3m);
        _balances.Single().Quantity.Should().Be(7m);
    }

    /// <summary>Stock that already matched needs no movement, and none is written.</summary>
    [Fact]
    public async Task CloseAsync_writes_nothing_when_the_stock_already_matches()
    {
        GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");

        await CreateService().CloseAsync(opened.Id, "user-2");

        _entries.Should().BeEmpty();
        _balances.Single().Quantity.Should().Be(10m);
    }

    /// <summary>The adjustment carries the count's reference, so the ledger says where it came from.</summary>
    [Fact]
    public async Task CloseAsync_names_the_count_on_the_adjustment()
    {
        GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");
        await CreateService().SetCountedAsync(opened.Id, [new CountedLineRequest(opened.Lines.Single().Id, 7m)]);

        await CreateService().CloseAsync(opened.Id, "user-2");

        _entries.Single().Reason.Should().Be("Inventário INV2026-01");
    }

    /// <summary>
    /// Half the adjustments landing would leave the warehouse in a state nobody asked for, so the
    /// close runs in one transaction.
    /// </summary>
    [Fact]
    public async Task CloseAsync_commits_everything_in_one_transaction()
    {
        GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");
        await CreateService().SetCountedAsync(opened.Id, [new CountedLineRequest(opened.Lines.Single().Id, 7m)]);

        await CreateService().CloseAsync(opened.Id, "user-2");

        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The balance is re-read under a lock at closing time, not taken from the picture at opening �?"
    /// that is what stops the count from undoing movements made while it was running.
    /// </summary>
    [Fact]
    public async Task CloseAsync_reads_the_balance_again_instead_of_trusting_the_opening_picture()
    {
        var balance = GivenStock();
        var opened = await CreateService().OpenAsync(Request(), "user-1");
        await CreateService().SetCountedAsync(opened.Id, [new CountedLineRequest(opened.Lines.Single().Id, 7m)]);

        // Something moved the stock while the count was open.
        balance.Apply(StockLedgerEntry.FromAdjustment(
            _companyId, _warehouseId, "ART001", "Artigo de teste", 2m,
            new DateOnly(2026, 6, 1), "Entrada durante a contagem", Guid.NewGuid(), "user-3"));

        await CreateService().CloseAsync(opened.Id, "user-2");

        // Counted 7 against a balance that had become 12, so the adjustment is -5, not -3.
        _entries.Should().ContainSingle().Which.Quantity.Should().Be(-5m);
        _balances.Single().Quantity.Should().Be(7m);
    }

    /// <summary>Zeroing is a count opened at zero: closing it empties the stock in scope.</summary>
    [Fact]
    public async Task Closing_a_zeroing_count_empties_the_stock_in_scope()
    {
        GivenStock();
        GivenStock("ART002", 4m);

        var opened = await CreateService().OpenAsync(Request(startAtZero: true), "user-1");
        await CreateService().CloseAsync(opened.Id, "user-2");

        _entries.Select(entry => entry.Quantity).Should().BeEquivalentTo([-10m, -4m]);
        _balances.Should().OnlyContain(balance => balance.Quantity == 0m);
    }

    [Fact]
    public async Task CloseAsync_returns_null_for_a_count_that_does_not_exist()
    {
        var result = await CreateService().CloseAsync(Guid.NewGuid(), "user-2");

        result.Should().BeNull();
    }
}
