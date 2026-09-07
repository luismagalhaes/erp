namespace Erp.Sales.Domain;

/// <summary>
/// What every append-only status transition has in common, whatever the document family. It lets
/// code that only needs to read the current status — the SAF-T export, for one — treat invoices,
/// movements and receipts the same way.
/// </summary>
public interface IStatusChange
{
    string PreviousStatus { get; }

    string NewStatus { get; }

    string Reason { get; }

    string? UserId { get; }

    DateTime OccurredAtUtc { get; }
}
