namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A row of the receipt list. Written with init members so it can be produced by an EF projection,
/// which is what the OData listing runs.
/// </summary>
public sealed record GoodsReceiptListItemDto
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly ReceiptDate { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string? SupplierDocumentNumber { get; init; }

    public int LineCount { get; init; }

    public decimal TotalCost { get; init; }

    public bool IsVoided { get; init; }
}
