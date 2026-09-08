using Erp.Common;
using Erp.Inventory.Application.Services;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Inventory.Tests;

public class StockServiceTests
{
    private readonly IStockStorage _storage = Substitute.For<IStockStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<StockBalance> _balances = [];
    private readonly List<StockLedgerEntry> _entries = [];

    public StockServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _storage.GetBalancesAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var warehouse = call.ArgAt<Guid?>(1);
                var product = call.ArgAt<string?>(2);

                return (IReadOnlyList<StockBalance>)
                [
                    .. _balances
                        .Where(x => x.CompanyId == call.ArgAt<Guid>(0))
                        .Where(x => warehouse == null || x.WarehouseId == warehouse)
                        .Where(x => product == null || x.ProductCode == product)
                ];
            });

        _storage.GetBalanceForUpdateAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _balances.FirstOrDefault(x =>
                x.CompanyId == call.ArgAt<Guid>(0)
                && x.WarehouseId == call.ArgAt<Guid>(1)
                && x.ProductCode == call.ArgAt<string>(2)));

        _storage.When(x => x.AddBalanceAsync(Arg.Any<StockBalance>(), Arg.Any<CancellationToken>()))
            .Do(call => _balances.Add(call.Arg<StockBalance>()));

        _storage.When(x => x.AddEntryAsync(Arg.Any<StockLedgerEntry>(), Arg.Any<CancellationToken>()))
            .Do(call => _entries.Add(call.Arg<StockLedgerEntry>()));

        _storage.GetEntriesAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var warehouse = call.ArgAt<Guid?>(1);
                var product = call.ArgAt<string?>(2);

                return (IReadOnlyList<StockLedgerEntry>)
                [
                    .. _entries
                        .Where(x => warehouse == null || x.WarehouseId == warehouse)
                        .Where(x => product == null || x.ProductCode == product)
                ];
            });

        // The ledger as the check reads it: the movements themselves, in order, so the check
        // replays them exactly as the real storage would hand them over.
        _storage.GetMovementsAsync(
                Arg.Any<Guid>(), Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var asAt = call.ArgAt<DateOnly?>(1);
                var product = call.ArgAt<string?>(2);

                return (IReadOnlyList<LedgerMovement>)
                [
                    .. _entries
                        .Where(x => asAt == null || x.MovementDate <= asAt)
                        .Where(x => product == null || x.ProductCode == product)
                        .OrderBy(x => x.SystemEntryDateUtc)
                        .ThenBy(x => x.Id)
                        .Select(x => new LedgerMovement(
                            x.WarehouseId, x.ProductCode, x.ProductDescription, x.Quantity, x.UnitCost))
                ];
            });
    }

    private StockService CreateService() => new(_storage, _unitOfWork);

    private AdjustStockRequest Adjustment(decimal difference, string productCode = "ART001") =>
        new(_companyId, _warehouseId, productCode, "Artigo de teste", difference,
            new DateOnly(2026, 1, 15), "Contagem");

    [Fact]
    public async Task AdjustAsync_starts_a_balance_when_the_product_has_none()
    {
        var balance = await CreateService().AdjustAsync(Adjustment(10m), "user-1");

        balance.Quantity.Should().Be(10m);
        _balances.Should().ContainSingle();
        _entries.Should().ContainSingle();
    }

    [Fact]
    public async Task AdjustAsync_moves_an_existing_balance()
    {
        var service = CreateService();

        await service.AdjustAsync(Adjustment(10m), "user-1");
        var balance = await service.AdjustAsync(Adjustment(-4m), "user-1");

        balance.Quantity.Should().Be(6m);
        _balances.Should().ContainSingle();
        _entries.Should().HaveCount(2);
    }

    /// <summary>
    /// Stock below zero is left visible rather than blocked: goods that went out without having
    /// come in is a real problem, and hiding it helps nobody.
    /// </summary>
    [Fact]
    public async Task AdjustAsync_allows_a_balance_to_go_negative()
    {
        var balance = await CreateService().AdjustAsync(Adjustment(-3m), "user-1");

        balance.Quantity.Should().Be(-3m);
    }

    [Fact]
    public async Task AdjustAsync_refuses_a_difference_of_zero()
    {
        var act = () => CreateService().AdjustAsync(Adjustment(0m), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*changes nothing*");
    }

    /// <summary>
    /// The balance row is locked before it is changed, so two movements of the same product cannot
    /// read the same figure and both write over it.
    /// </summary>
    [Fact]
    public async Task AdjustAsync_locks_the_balance_before_changing_it()
    {
        await CreateService().AdjustAsync(Adjustment(1m), "user-1");

        Received.InOrder(() =>
        {
            _storage.GetBalanceForUpdateAsync(
                _companyId, _warehouseId, "ART001", Arg.Any<CancellationToken>());
            _storage.AddEntryAsync(Arg.Any<StockLedgerEntry>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GetLedgerAsync_carries_a_running_balance()
    {
        var service = CreateService();

        await service.AdjustAsync(Adjustment(10m), "user-1");
        await service.AdjustAsync(Adjustment(-4m), "user-1");
        await service.AdjustAsync(Adjustment(2m), "user-1");

        var ledger = await service.GetLedgerAsync(_companyId, _warehouseId, "ART001");

        ledger.Select(x => x.RunningBalance).Should().ContainInOrder(10m, 6m, 8m);
    }

    [Fact]
    public async Task CheckAsync_reports_no_difference_when_the_balance_matches_the_ledger()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m), "user-1");

        var result = await service.CheckAsync(_companyId);

        result.ProductsWithDifference.Should().Be(0);
        result.Lines.Should().ContainSingle().Which.Difference.Should().Be(0m);
    }

    /// <summary>
    /// The whole point of keeping both the ledger and the balance: a balance changed without a
    /// matching entry is exactly what the check has to surface.
    /// </summary>
    [Fact]
    public async Task CheckAsync_reports_a_balance_that_drifted_from_the_ledger()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m), "user-1");

        // Something wrote outside the normal path: the entry vanished, the balance did not.
        _entries.Clear();

        var result = await service.CheckAsync(_companyId);

        result.ProductsWithDifference.Should().Be(1);
        var line = result.Lines.Should().ContainSingle().Subject;
        line.RecordedQuantity.Should().Be(10m);
        line.LedgerQuantity.Should().Be(0m);
        line.Difference.Should().Be(10m);
    }

    /// <summary>Stock moved with nothing recording it is the worse failure, so it is reported too.</summary>
    [Fact]
    public async Task CheckAsync_reports_ledger_entries_with_no_balance_at_all()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m), "user-1");

        _balances.Clear();

        var result = await service.CheckAsync(_companyId);

        var line = result.Lines.Should().ContainSingle().Subject;
        line.RecordedQuantity.Should().Be(0m);
        line.LedgerQuantity.Should().Be(10m);
        line.Difference.Should().Be(-10m);
    }

    [Fact]
    public async Task CheckAsync_can_be_narrowed_to_one_product()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m), "user-1");
        await service.AdjustAsync(Adjustment(5m, "ART002"), "user-1");

        var result = await service.CheckAsync(_companyId, "ART002");

        result.Lines.Should().ContainSingle().Which.ProductCode.Should().Be("ART002");
    }

    // --- Costing ---

    /// <summary>
    /// An opening adjustment is the only way a company that starts with a full warehouse gets its
    /// cost into the system, so the adjustment is allowed to carry one.
    /// </summary>
    [Fact]
    public async Task AdjustAsync_takes_the_cost_the_adjustment_carries()
    {
        var balance = await CreateService().AdjustAsync(Adjustment(10m) with { UnitCost = 4m }, "user-1");

        balance.AverageCost.Should().Be(4m);
        balance.StockValue.Should().Be(40m);
    }

    /// <summary>
    /// An ordinary count finds goods, not prices: what turns up is worth the average of what was
    /// already there.
    /// </summary>
    [Fact]
    public async Task AdjustAsync_values_a_find_at_the_average_when_it_carries_no_cost()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m) with { UnitCost = 4m }, "user-1");

        var balance = await service.AdjustAsync(Adjustment(5m), "user-1");

        balance.Quantity.Should().Be(15m);
        balance.AverageCost.Should().Be(4m);
        balance.StockValue.Should().Be(60m);
    }

    /// <summary>
    /// The ledger says what each movement did to the value, not merely to the quantity — which is
    /// what lets it be replayed and what lets a reversal undo exactly what it did.
    /// </summary>
    [Fact]
    public async Task AdjustAsync_stamps_the_entry_with_the_cost_it_moved_at()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m) with { UnitCost = 4m }, "user-1");
        await service.AdjustAsync(Adjustment(-2m), "user-1");

        _entries.Select(x => x.UnitCost).Should().Equal(4m, 4m);
    }

    /// <summary>
    /// The stored value and the replay have to agree, or the check would be comparing a projection
    /// against itself. Two purchases at different prices is the case that would drift first.
    /// </summary>
    [Fact]
    public async Task CheckAsync_finds_the_stored_value_agreeing_with_the_replayed_ledger()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m) with { UnitCost = 10m }, "user-1");
        await service.AdjustAsync(Adjustment(10m) with { UnitCost = 20m }, "user-1");
        await service.AdjustAsync(Adjustment(-5m), "user-1");

        var result = await service.CheckAsync(_companyId);

        var line = result.Lines.Should().ContainSingle().Subject;
        line.RecordedValue.Should().Be(225m);
        line.LedgerValue.Should().Be(225m);
        line.ValueDifference.Should().Be(0m);
        result.ProductsWithValueDifference.Should().Be(0);
    }

    /// <summary>
    /// A value can drift while the quantity still agrees — the average depends on the order the
    /// movements arrived in, not on their sum — so the two are counted separately.
    /// </summary>
    [Fact]
    public async Task CheckAsync_reports_a_value_that_drifted_while_the_quantity_still_agrees()
    {
        var service = CreateService();
        await service.AdjustAsync(Adjustment(10m) with { UnitCost = 10m }, "user-1");

        // The entry keeps the quantity but forgets what it cost, as an entry written before
        // costing existed would.
        var costless = StockLedgerEntry.FromAdjustment(
            _companyId, _warehouseId, "ART001", "Artigo de teste", 10m, new DateOnly(2026, 1, 15), "Sem custo");

        _entries.Clear();
        _entries.Add(costless);

        var result = await service.CheckAsync(_companyId);

        var line = result.Lines.Should().ContainSingle().Subject;
        line.Difference.Should().Be(0m, "the quantity still adds up");
        line.RecordedValue.Should().Be(100m);
        line.LedgerValue.Should().Be(0m);
        result.ProductsWithValueDifference.Should().Be(1);
    }
}
