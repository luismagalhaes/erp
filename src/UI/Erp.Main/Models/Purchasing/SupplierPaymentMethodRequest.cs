namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPaymentMethodRequest(string Mechanism, decimal Amount, DateOnly PaymentDate);
