namespace Erp.Main.Models.Purchasing;

/// <summary>
/// A supplier document still open: an invoice with money owed, or a credit note with credit left.
/// </summary>
public sealed record PayableDocument(
    string DocumentKind,
    Guid DocumentId,
    string DocumentType,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    bool IsCredit,
    decimal GrossTotal,
    decimal PaidAmount,
    decimal OutstandingAmount);
