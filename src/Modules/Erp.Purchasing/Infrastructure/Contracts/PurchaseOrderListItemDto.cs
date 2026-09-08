namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A row of the order list. Deliberately without lines, which the list never shows. Written with
/// init members so it can be produced by an EF projection, which is what the OData listing runs.
/// </summary>
public sealed record PurchaseOrderListItemDto
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly OrderDate { get; init; }

    public DateOnly? ExpectedDate { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string SupplierTaxId { get; init; } = string.Empty;

    public int LineCount { get; init; }

    public decimal GrossTotal { get; init; }

    public bool IsOpen { get; init; }
}
