namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A row of the supplier invoice list. Written with init members so it can be produced by an EF
/// projection, which is what the OData listing runs.
/// </summary>
public sealed record PurchaseInvoiceListItemDto
{
    public Guid Id { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string SupplierDocumentNumber { get; init; } = string.Empty;

    public DateOnly SupplierDocumentDate { get; init; }

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string SupplierTaxId { get; init; } = string.Empty;

    public decimal NetTotal { get; init; }

    public decimal TaxTotal { get; init; }

    public decimal GrossTotal { get; init; }

    public bool ReverseCharge { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsVoided { get; init; }
}
