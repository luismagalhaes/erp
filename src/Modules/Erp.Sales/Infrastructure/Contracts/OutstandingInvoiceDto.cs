namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>An issued invoice with money still owed on it, ready to be settled by a receipt.</summary>
/// <param name="DocumentId">
/// The settled document. Named for what it is, because a receipt line refers to it by this id.
/// </param>
public sealed record OutstandingInvoiceDto(
    Guid DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    string CustomerName,
    string CustomerTaxId,
    decimal GrossTotal,
    decimal SettledAmount,
    decimal OutstandingAmount);
