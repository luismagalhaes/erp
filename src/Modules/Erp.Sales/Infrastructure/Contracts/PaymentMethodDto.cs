namespace Erp.Sales.Infrastructure.Contracts;

public sealed record PaymentMethodDto(string Mechanism, decimal Amount, DateOnly PaymentDate);
