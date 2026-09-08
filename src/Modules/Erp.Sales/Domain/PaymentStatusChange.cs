namespace Erp.Sales.Domain;

/// <summary>
/// Append-only record of a receipt status transition. Voiding is written here instead of
/// updating the receipt header, which keeps the original record intact.
/// </summary>
public sealed class PaymentStatusChange : IStatusChange
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; } = null!;
}
