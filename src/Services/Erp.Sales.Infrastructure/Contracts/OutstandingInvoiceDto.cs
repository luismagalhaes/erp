namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>An issued invoice with money still owed on it, ready to be settled by a receipt.</summary>
public sealed record OutstandingInvoiceDto(
    Guid Id,
    string DocumentNumber,
    DateOnly DocumentDate,
    string CustomerName,
    string CustomerTaxId,
    decimal GrossTotal,
    decimal SettledAmount,
    decimal OutstandingAmount);
