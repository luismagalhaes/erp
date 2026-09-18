namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="Mechanism">The same mechanisms as a receipt: NU, CH, TB, ...</param>
public sealed record SupplierPaymentMethodRequest(string Mechanism, decimal Amount, DateOnly PaymentDate);
