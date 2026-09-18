namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record SupplierPaymentMethodDto(string Mechanism, decimal Amount, DateOnly PaymentDate);
