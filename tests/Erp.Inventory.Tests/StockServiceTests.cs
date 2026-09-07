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
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<StockBalance> _balances = [];
    private readonly List<StockLedgerEntry> _entries = [];

    public StockServiceTests()
    {
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

        _storage.SumLedgerAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var product = call.ArgAt<string?>(1);

                return (IReadOnlyDictionary<(Guid, string), (decimal, int)>)_entries
                    .Where(x => product == null || x.ProductCode == product)
                    .GroupBy(x => (x.WarehouseId, x.ProductCode))
                    .ToDictionary(g => g.Key, g => (g.Sum(x => x.Quantity), g.Count()));
            });
    }

    private StockService CreateService() => new(_storage);

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
}
