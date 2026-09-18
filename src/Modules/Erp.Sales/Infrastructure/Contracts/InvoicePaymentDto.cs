namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>How a fatura-recibo was paid.</summary>
public sealed record InvoicePaymentDto(string Mechanism, decimal Amount, DateOnly PaymentDate);
