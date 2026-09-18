namespace Erp.Main.Models.Sales;

/// <summary>How a fatura-recibo was paid.</summary>
public sealed record InvoicePayment(string Mechanism, decimal Amount, DateOnly PaymentDate);
