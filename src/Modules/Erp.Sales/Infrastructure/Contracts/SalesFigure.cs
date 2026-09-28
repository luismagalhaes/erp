namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// The few columns of an issued document that sales analysis reads, so a period can be aggregated
/// without loading lines, taxes and status changes. Voided documents never appear as a figure.
/// </summary>
public sealed record SalesFigure(
    DateOnly DocumentDate,
    string DocumentType,
    string CustomerName,
    string CustomerTaxId,
    decimal NetTotal,
    decimal GrossTotal);
