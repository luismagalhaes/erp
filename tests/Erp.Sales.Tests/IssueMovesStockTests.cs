using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using NSubstitute;
using Erp.SeriesRegistry.Domain;

namespace Erp.Sales.Tests;

/// <summary>
/// Issuing a document records the stock it moves, inside the transaction that writes the document.
/// What the recorder then does with it is tested in Erp.Inventory.Tests.
/// </summary>
public class IssueMovesStockTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private CreateInvoiceRequest Request(Guid seriesId, Guid? warehouseId) =>
        SalesTestContext.InvoiceRequest(_companyId, seriesId) with { WarehouseId = warehouseId };

    [Fact]
    public async Task IssueAsync_records_the_stock_a_selling_series_moves()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.Out);

        await _context.CreateService().IssueAsync(Request(series.Id, _warehouseId), "user-1");

        await _context.StockRecorder.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(r =>
                r.CompanyId == _companyId
                && r.WarehouseId == _warehouseId
                && r.Direction == StockDirection.Out
                && r.Lines.Count == 1),
            "user-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_records_an_in_for_a_series_that_brings_stock_back()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.In);

        await _context.CreateService().IssueAsync(Request(series.Id, _warehouseId), "user-1");

        await _context.StockRecorder.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(r => r.Direction == StockDirection.In),
            "user-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_records_nothing_for_a_series_that_moves_no_stock()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.None);

        await _context.CreateService().IssueAsync(Request(series.Id, _warehouseId), "user-1");

        await _context.StockRecorder.DidNotReceive().RecordAsync(
            Arg.Any<RecordDocumentStockRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A series that moves stock without a warehouse has nowhere to put it, and guessing would be
    /// worse than refusing.
    /// </summary>
    [Fact]
    public async Task IssueAsync_refuses_to_move_stock_without_a_warehouse()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.Out);

        var act = () => _context.CreateService().IssueAsync(Request(series.Id, null), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*needs a warehouse*");
    }

    /// <summary>
    /// The stock is recorded before the commit, so document and stock land together or not at all.
    /// </summary>
    [Fact]
    public async Task IssueAsync_records_the_stock_before_committing()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.Out);

        await _context.CreateService().IssueAsync(Request(series.Id, _warehouseId), "user-1");

        Received.InOrder(() =>
        {
            _context.StockRecorder.RecordAsync(
                Arg.Any<RecordDocumentStockRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
            _context.Transaction.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// The lines carry where they came from, which is what lets the recorder leave alone the goods
    /// a delivery note already moved.
    /// </summary>
    [Fact]
    public async Task IssueAsync_passes_the_originating_line_to_the_recorder()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, stockEffect: StockEffect.Out);
        var movementLineId = Guid.NewGuid();

        var request = Request(series.Id, _warehouseId) with
        {
            Lines = [SalesTestContext.Line() with { OriginatingLineId = movementLineId }]
        };

        // The origin has to resolve, so the movement it belongs to is served to the service.
        _context.GivenInvoiceableMovementLine(_companyId, movementLineId);

        await _context.CreateService().IssueAsync(request, "user-1");

        await _context.StockRecorder.Received(1).RecordAsync(
            Arg.Is<RecordDocumentStockRequest>(r => r.Lines[0].OriginatingLineId == movementLineId),
            "user-1",
            Arg.Any<CancellationToken>());
    }
}
