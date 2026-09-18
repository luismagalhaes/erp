namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPaymentLine(
    int LineNumber,
    string DocumentKind,
    Guid DocumentId,
    string DocumentType,
    string DocumentNumber,
    DateOnly DocumentDate,
    bool IsCredit,
    decimal AppliedAmount);
