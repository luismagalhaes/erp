namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// The listing shape of a sales document. Written with init members rather than as a positional
/// record so it can be produced by an EF projection, which is what the OData listing runs.
/// </summary>
public sealed record InvoiceListItemDto
{
    public Guid Id { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public string DocumentType { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public DateOnly DocumentDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerTaxId { get; init; } = string.Empty;

    public decimal NetTotal { get; init; }

    public decimal TaxPayable { get; init; }

    public decimal GrossTotal { get; init; }

    public string Status { get; init; } = string.Empty;
}
