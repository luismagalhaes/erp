namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A row of the supplier return list. Written with init members so it can be produced by an EF
/// projection, which is what the OData listing runs.
/// </summary>
public sealed record SupplierReturnListItemDto
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly ReturnDate { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public int LineCount { get; init; }

    public decimal TotalCost { get; init; }

    public bool IsVoided { get; init; }
}
