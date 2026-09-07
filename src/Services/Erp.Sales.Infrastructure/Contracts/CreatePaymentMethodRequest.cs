namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="Mechanism">SAF-T PaymentMechanism: NU, CH, CD, CC, TB, ...</param>
public sealed record CreatePaymentMethodRequest(string Mechanism, decimal Amount, DateOnly PaymentDate);
