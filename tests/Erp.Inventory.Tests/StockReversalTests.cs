using Erp.Inventory.Application.Services;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Inventory.Tests;

/// <summary>
/// Voiding a document puts back the stock it moved. The ledger is append-only, so nothing is
/// removed: the opposite entry is added and both stay visible.
/// </summary>
public class StockReversalTests
{
    private readonly IStockStorage _storage = Substitute.For<IStockStorage>();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();

    private readonly List<StockLedgerEntry> _entries = [];
    private readonly List<StockBalance> _balances = [];

    public StockReversalTests()
    {
        _storage.When(x => x.AddEntryAsync(Arg.Any<StockLedgerEntry>(), Arg.Any<CancellationToken>()))
            .Do(call => _entries.Add(call.Arg<StockLedgerEntry>()));

        _storage.When(x => x.AddBalanceAsync(Arg.Any<StockBalance>(), Arg.Any<CancellationToken>()))
            .Do(call => _balances.Add(call.Arg<StockBalance>()));

        _storage.GetBalanceForUpdateAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _balances.FirstOrDefault(x => x.ProductCode == call.ArgAt<string>(2)));

        _storage.GetEntriesForDocumentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<StockLedgerEntry>)
                [.. _entries.Where(x => x.SourceDocumentId == call.ArgAt<Guid>(0))]);

        _storage.HasEntryForLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private StockRecorder CreateRecorder() => new(_storage);

    /// <summary>Issues a document that takes five units out, as a delivery note would.</summary>
    private async Task GivenDocumentTookStockOutAsync(decimal quantity = 5m)
    {
        var request = new RecordDocumentStockRequest(
            _companyId, _warehouseId, StockDirection.Out, new DateOnly(2026, 1, 15),
            "GR", "GR A2026/1", _documentId,
            [new DocumentStockLine(Guid.NewGuid(), "ART001", "Artigo de teste", quantity)]);

        await CreateRecorder().RecordAsync(request, "user-1");
    }

    [Fact]
    public async Task ReverseDocumentAsync_puts_the_stock_back()
    {
        await GivenDocumentTookStockOutAsync();
        _balances.Should().ContainSingle().Which.Quantity.Should().Be(-5m);

        var reversed = await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        reversed.Should().Be(1);
        _balances.Should().ContainSingle().Which.Quantity.Should().Be(0m);
    }

    /// <summary>Nothing is deleted: the movement and its undoing both stay in the ledger.</summary>
    [Fact]
    public async Task ReverseDocumentAsync_adds_an_entry_instead_of_removing_one()
    {
        await GivenDocumentTookStockOutAsync();

        await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        _entries.Should().HaveCount(2);
        _entries[0].Quantity.Should().Be(-5m);
        _entries[1].Quantity.Should().Be(5m);
        _entries[1].Reason.Should().Be("Anulação");
    }

    /// <summary>
    /// The reversal carries no line of its own: that column means "this line has moved its stock",
    /// which stays true of the original even after the document is voided.
    /// </summary>
    [Fact]
    public async Task ReverseDocumentAsync_leaves_the_source_line_on_the_original_only()
    {
        await GivenDocumentTookStockOutAsync();

        await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        _entries[0].SourceLineId.Should().NotBeNull();
        _entries[1].SourceLineId.Should().BeNull();
    }

    [Fact]
    public async Task ReverseDocumentAsync_flips_the_direction()
    {
        await GivenDocumentTookStockOutAsync();

        await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        _entries[1].Direction.Should().Be(StockDirection.In);
    }

    /// <summary>Voiding twice must not put the goods back twice.</summary>
    [Fact]
    public async Task ReverseDocumentAsync_does_nothing_the_second_time()
    {
        await GivenDocumentTookStockOutAsync();

        var first = await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");
        var second = await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        first.Should().Be(1);
        second.Should().Be(0);
        _balances.Should().ContainSingle().Which.Quantity.Should().Be(0m);
    }

    [Fact]
    public async Task ReverseDocumentAsync_does_nothing_for_a_document_that_moved_no_stock()
    {
        var reversed = await CreateRecorder().ReverseDocumentAsync(Guid.NewGuid(), "Anulação", "user-2");

        reversed.Should().Be(0);
        await _storage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReverseDocumentAsync_reverses_every_line_of_the_document()
    {
        var request = new RecordDocumentStockRequest(
            _companyId, _warehouseId, StockDirection.Out, new DateOnly(2026, 1, 15),
            "GR", "GR A2026/1", _documentId,
            [
                new DocumentStockLine(Guid.NewGuid(), "ART001", "Artigo um", 5m),
                new DocumentStockLine(Guid.NewGuid(), "ART002", "Artigo dois", 3m)
            ]);

        await CreateRecorder().RecordAsync(request, "user-1");

        var reversed = await CreateRecorder().ReverseDocumentAsync(_documentId, "Anulação", "user-2");

        reversed.Should().Be(2);
        _balances.Should().OnlyContain(x => x.Quantity == 0m);
    }

    [Fact]
    public async Task ReverseDocumentAsync_requires_a_reason()
    {
        await GivenDocumentTookStockOutAsync();

        var act = () => CreateRecorder().ReverseDocumentAsync(_documentId, "   ", "user-2");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
