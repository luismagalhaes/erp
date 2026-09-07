using Erp.Inventory.Infrastructure.Contracts;

namespace Erp.Inventory.Infrastructure.Application;

/// <summary>
/// Records the stock a document moves. Called by whoever issues the document, inside the same
/// transaction, so the document and the stock commit together.
/// </summary>
public interface IStockRecorder
{
    /// <summary>
    /// Writes one ledger entry per line that still has stock to move, and returns how many were
    /// written. Lines whose goods already moved — because the delivery note that preceded the
    /// invoice moved them — are skipped rather than refused: an invoice that follows a delivery
    /// note is normal, it simply has nothing left to move.
    /// </summary>
    Task<int> RecordAsync(
        RecordDocumentStockRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes what a document moved, by recording the opposite of each entry. Called when the
    /// document is voided: goods that never left have to come back into the warehouse.
    /// </summary>
    /// <remarks>
    /// Nothing is deleted. The original entries stay, and the reversals sit beside them, so the
    /// ledger still tells the whole story. Reversing twice is refused by counting what is already
    /// undone, so a repeated void cannot inflate the stock.
    /// </remarks>
    /// <returns>How many entries were reversed.</returns>
    Task<int> ReverseDocumentAsync(
        Guid documentId,
        string reason,
        string? userId,
        CancellationToken cancellationToken = default);
}
