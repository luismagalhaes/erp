namespace Erp.Purchasing.Domain;

/// <summary>
/// Append-only record of a status transition. Voiding is written here instead of updating the
/// document header, which keeps the original record intact.
/// </summary>
public sealed class SelfBilledInvoiceStatusChange
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public SelfBilledInvoice Invoice { get; set; } = null!;
}
