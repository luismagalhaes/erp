namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPaymentMethod(string Mechanism, decimal Amount, DateOnly PaymentDate);
