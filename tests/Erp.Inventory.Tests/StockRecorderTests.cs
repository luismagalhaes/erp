using Erp.Inventory.Application.Services;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Inventory.Tests;

/// <summary>
/// Recording the stock a document moves, and above all not moving it twice: a delivery note takes
/// the goods out, and the invoice raised from that note must leave them alone.
/// </summary>
public class StockRecorderTests
{
    private readonly IStockStorage _storage = Substitute.For<IStockStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<StockLedgerEntry> _entries = [];
    private readonly List<StockBalance> _balances = [];

    public StockRecorderTests()
    {
        _storage.When(x => x.AddEntryAsync(Arg.Any<StockLedgerEntry>(), Arg.Any<CancellationToken>()))
            .Do(call => _entries.Add(call.Arg<StockLedgerEntry>()));

        _storage.When(x => x.AddBalanceAsync(Arg.Any<StockBalance>(), Arg.Any<CancellationToken>()))
            .Do(call => _balances.Add(call.Arg<StockBalance>()));

        _storage.GetBalanceForUpdateAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _balances.FirstOrDefault(x => x.ProductCode == call.ArgAt<string>(2)));

        // By default nothing has moved yet; individual tests say otherwise.
        _storage.HasEntryForLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private StockRecorder CreateRecorder() => new(_storage);

    private RecordDocumentStockRequest Request(
        StockDirection direction,
        params DocumentStockLine[] lines) =>
        new(_companyId,
            _warehouseId,
            direction,
            new DateOnly(2026, 1, 15),
            "GR",
            "GR A2026/1",
            Guid.NewGuid(),
            lines);

    private static DocumentStockLine Line(
        decimal quantity = 5m,
        Guid? originatingLineId = null,
        decimal? unitCost = null) =>
        new(Guid.NewGuid(), "ART001", "Artigo de teste", quantity, originatingLineId, unitCost);

    [Fact]
    public async Task RecordAsync_writes_an_entry_per_line()
    {
        var recorded = await CreateRecorder().RecordAsync(
            Request(StockDirection.Out, Line(), Line(3m)), "user-1");

        recorded.Should().Be(2);
        _entries.Should().HaveCount(2);
        _balances.Should().ContainSingle().Which.Quantity.Should().Be(-8m);
    }

    /// <summary>
    /// The requirement in one test: the guia moved the goods, so the invoice raised from it does
    /// not move them again.
    /// </summary>
    [Fact]
    public async Task RecordAsync_skips_a_line_whose_origin_already_moved_the_stock()
    {
        var deliveryNoteLineId = Guid.NewGuid();
        _storage.HasEntryForLineAsync(deliveryNoteLineId, Arg.Any<CancellationToken>()).Returns(true);

        var recorded = await CreateRecorder().RecordAsync(
            Request(StockDirection.Out, Line(originatingLineId: deliveryNoteLineId)), "user-1");

        recorded.Should().Be(0);
        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_moves_a_line_whose_origin_has_not_moved_yet()
    {
        // A guia issued from a series with no stock effect leaves the invoice to do the moving.
        var recorded = await CreateRecorder().RecordAsync(
            Request(StockDirection.Out, Line(originatingLineId: Guid.NewGuid())), "user-1");

        recorded.Should().Be(1);
    }

    /// <summary>Re-issuing the same document must not double the movement either.</summary>
    [Fact]
    public async Task RecordAsync_skips_a_line_that_has_already_moved_itself()
    {
        var line = Line();
        _storage.HasEntryForLineAsync(line.DocumentLineId, Arg.Any<CancellationToken>()).Returns(true);

        var recorded = await CreateRecorder().RecordAsync(Request(StockDirection.Out, line), "user-1");

        recorded.Should().Be(0);
    }

    [Fact]
    public async Task RecordAsync_moves_only_the_lines_that_still_have_something_to_move()
    {
        var moved = Guid.NewGuid();
        _storage.HasEntryForLineAsync(moved, Arg.Any<CancellationToken>()).Returns(true);

        var recorded = await CreateRecorder().RecordAsync(
            Request(StockDirection.Out, Line(originatingLineId: moved), Line(2m)), "user-1");

        recorded.Should().Be(1);
        _entries.Should().ContainSingle().Which.Quantity.Should().Be(-2m);
    }

    [Fact]
    public async Task RecordAsync_signs_an_in_as_positive()
    {
        await CreateRecorder().RecordAsync(Request(StockDirection.In, Line(4m)), "user-1");

        _balances.Should().ContainSingle().Which.Quantity.Should().Be(4m);
    }

    [Fact]
    public async Task RecordAsync_saves_nothing_when_every_line_was_skipped()
    {
        var moved = Guid.NewGuid();
        _storage.HasEntryForLineAsync(moved, Arg.Any<CancellationToken>()).Returns(true);

        await CreateRecorder().RecordAsync(
            Request(StockDirection.Out, Line(originatingLineId: moved)), "user-1");

        await _storage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_ignores_a_line_with_no_quantity()
    {
        var recorded = await CreateRecorder().RecordAsync(Request(StockDirection.Out, Line(0m)), "user-1");

        recorded.Should().Be(0);
    }

    [Fact]
    public async Task RecordAsync_refuses_a_document_without_a_warehouse()
    {
        var request = new RecordDocumentStockRequest(
            _companyId, Guid.Empty, StockDirection.Out, new DateOnly(2026, 1, 15),
            "GR", "GR A2026/1", Guid.NewGuid(), [Line()]);

        var act = () => CreateRecorder().RecordAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*needs a warehouse*");
    }

    [Fact]
    public async Task RecordAsync_refuses_an_adjustment()
    {
        var act = () => CreateRecorder().RecordAsync(Request(StockDirection.Adjustment, Line()), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*never as an adjustment*");
    }

    // --- Costing ---

    /// <summary>
    /// A purchase brings its own price; a sale carries none and leaves at the average. That split
    /// is the whole of the costing, and it is what the two modules already do without knowing it:
    /// Purchasing passes a cost, Sales does not.
    /// </summary>
    [Fact]
    public async Task RecordAsync_takes_a_sale_out_at_the_average_of_what_was_bought()
    {
        var recorder = CreateRecorder();

        await recorder.RecordAsync(Request(StockDirection.In, Line(10m, unitCost: 10m)), "user-1");
        await recorder.RecordAsync(Request(StockDirection.In, Line(10m, unitCost: 20m)), "user-1");
        await recorder.RecordAsync(Request(StockDirection.Out, Line(5m)), "user-1");

        var balance = _balances.Should().ContainSingle().Subject;
        balance.Quantity.Should().Be(15m);
        balance.AverageCost.Should().Be(15m);
        balance.StockValue.Should().Be(225m);
    }

    /// <summary>
    /// The sale is stamped with the average it left at, so the ledger says what it did to the value
    /// and not merely to the quantity.
    /// </summary>
    [Fact]
    public async Task RecordAsync_stamps_a_sale_with_the_average_it_left_at()
    {
        var recorder = CreateRecorder();

        await recorder.RecordAsync(Request(StockDirection.In, Line(10m, unitCost: 12m)), "user-1");
        await recorder.RecordAsync(Request(StockDirection.Out, Line(4m)), "user-1");

        _entries.Select(x => x.UnitCost).Should().Equal(12m, 12m);
    }

    /// <summary>
    /// Voiding a purchase takes back exactly what it brought. Undoing it at today's average would
    /// leave the goods bought at the other price worth the wrong amount — the classic drift of the
    /// method, and the reason the reversal carries the original cost.
    /// </summary>
    [Fact]
    public async Task ReverseDocumentAsync_undoes_a_purchase_at_the_price_it_was_bought_at()
    {
        var recorder = CreateRecorder();

        var cheap = Request(StockDirection.In, Line(10m, unitCost: 5m));
        await recorder.RecordAsync(cheap, "user-1");
        await recorder.RecordAsync(Request(StockDirection.In, Line(10m, unitCost: 7m)), "user-1");

        _balances.Single().AverageCost.Should().Be(6m);

        _storage.GetEntriesForDocumentAsync(cheap.DocumentId, Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<StockLedgerEntry>)[.. _entries.Where(x => x.SourceDocumentId == cheap.DocumentId)]);

        await recorder.ReverseDocumentAsync(cheap.DocumentId, "Anulação", "user-2");

        var balance = _balances.Single();
        balance.Quantity.Should().Be(10m);
        balance.StockValue.Should().Be(70m);
        balance.AverageCost.Should().Be(7m, "the ten units left are the ones bought at 7");
    }
}
