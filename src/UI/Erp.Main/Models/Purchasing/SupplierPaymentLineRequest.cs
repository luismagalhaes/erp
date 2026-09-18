namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPaymentLineRequest(string DocumentKind, Guid DocumentId, decimal AppliedAmount);
