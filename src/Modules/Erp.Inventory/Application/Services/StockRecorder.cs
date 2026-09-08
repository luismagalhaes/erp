using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;

namespace Erp.Inventory.Application.Services;

public sealed class StockRecorder(IStockStorage storage) : IStockRecorder
{
    public async Task<int> RecordAsync(
        RecordDocumentStockRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Direction == StockDirection.Adjustment)
            throw new ArgumentException("A document moves stock in or out, never as an adjustment.", nameof(request));

        if (request.WarehouseId == Guid.Empty)
            throw new ArgumentException("A stock movement needs a warehouse.", nameof(request));

        var recorded = 0;

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                continue;

            if (await AlreadyMovedAsync(line, cancellationToken))
                continue;

            var entry = StockLedgerEntry.FromDocument(
                request.CompanyId,
                request.WarehouseId,
                line.ProductCode,
                line.ProductDescription,
                request.Direction,
                line.Quantity,
                request.MovementDate,
                request.DocumentType,
                request.DocumentNumber,
                request.DocumentId,
                line.DocumentLineId,
                line.UnitCost,
                userId);

            await ApplyAsync(entry, cancellationToken);
            recorded++;
        }

        if (recorded > 0)
            await storage.SaveChangesAsync(cancellationToken);

        return recorded;
    }

    public async Task<int> ReverseDocumentAsync(
        Guid documentId,
        string reason,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var entries = await storage.GetEntriesForDocumentAsync(documentId, cancellationToken);

        if (entries.Count == 0)
            return 0;

        // The document's entries include any reversals already recorded against it. Pairing them
        // off is what stops a second void from putting the goods back a second time.
        var reversals = entries.Where(IsReversal).ToList();
        var originals = entries.Where(entry => !IsReversal(entry)).ToList();

        var reversed = 0;

        foreach (var original in originals)
        {
            var alreadyUndone = reversals.Find(x =>
                x.ProductCode == original.ProductCode
                && x.WarehouseId == original.WarehouseId
                && x.Quantity == -original.Quantity);

            if (alreadyUndone is not null)
            {
                reversals.Remove(alreadyUndone);
                continue;
            }

            await ApplyAsync(StockLedgerEntry.Reverse(original, reason, userId), cancellationToken);
            reversed++;
        }

        if (reversed > 0)
            await storage.SaveChangesAsync(cancellationToken);

        return reversed;
    }

    /// <summary>A reversal is the entry that carries a reason and no line of its own.</summary>
    private static bool IsReversal(StockLedgerEntry entry) =>
        entry.SourceLineId is null && !string.IsNullOrWhiteSpace(entry.Reason);

    /// <summary>
    /// The integrating document rule: goods delivered on a guia are not taken out again when the
    /// invoice for that guia is issued. The line carries where it came from, and if that line has
    /// a ledger entry the stock is already gone.
    /// </summary>
    private async Task<bool> AlreadyMovedAsync(DocumentStockLine line, CancellationToken cancellationToken)
    {
        // Guards a retry of the same issue as well: a line moves stock once, whatever the reason.
        if (await storage.HasEntryForLineAsync(line.DocumentLineId, cancellationToken))
            return true;

        return line.OriginatingLineId is { } originating
               && await storage.HasEntryForLineAsync(originating, cancellationToken);
    }

    /// <summary>Writes the entry and moves the balance with it, locking the balance row first.</summary>
    private async Task ApplyAsync(StockLedgerEntry entry, CancellationToken cancellationToken)
    {
        var balance = await storage.GetBalanceForUpdateAsync(
            entry.CompanyId, entry.WarehouseId, entry.ProductCode, cancellationToken);

        if (balance is null)
        {
            balance = StockBalance.Start(entry.CompanyId, entry.WarehouseId, entry.ProductCode, entry.ProductDescription);
            await storage.AddBalanceAsync(balance, cancellationToken);
        }

        balance.Apply(entry);

        await storage.AddEntryAsync(entry, cancellationToken);
    }
}
