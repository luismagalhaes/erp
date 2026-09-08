namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// The listing shape of a receipt. Written with init members rather than as a positional record so
/// it can be produced by an EF projection, which is what the OData listing runs.
/// </summary>
public sealed record PaymentListItemDto
{
    public Guid Id { get; init; }

    public string PaymentRefNo { get; init; } = string.Empty;

    public string PaymentType { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    public string PartyName { get; init; } = string.Empty;

    public string PartyTaxId { get; init; } = string.Empty;

    public decimal GrossTotal { get; init; }

    public int SettledInvoiceCount { get; init; }

    public string Status { get; init; } = string.Empty;
}
