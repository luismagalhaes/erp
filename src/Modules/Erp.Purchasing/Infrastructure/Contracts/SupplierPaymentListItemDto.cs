namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A row of the supplier payment list. Written with init members so it can be produced by an EF
/// projection, which is what the OData listing runs.
/// </summary>
public sealed record SupplierPaymentListItemDto
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public DateOnly PaymentDate { get; init; }

    public Guid SupplierId { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string SupplierTaxId { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public int SettledDocumentCount { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsVoided { get; init; }
}
