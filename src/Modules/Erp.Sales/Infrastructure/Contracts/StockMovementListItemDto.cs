namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// The listing shape of a goods movement. Written with init members rather than as a positional
/// record so it can be produced by an EF projection, which is what the OData listing runs.
/// </summary>
public sealed record StockMovementListItemDto
{
    public Guid Id { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public string MovementType { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public DateOnly MovementDate { get; init; }

    public string PartyName { get; init; } = string.Empty;

    public string PartyTaxId { get; init; } = string.Empty;

    public DateTime MovementStartAtUtc { get; init; }

    public decimal TotalQuantity { get; init; }

    public decimal GrossTotal { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? AtDocCodeId { get; init; }
}
