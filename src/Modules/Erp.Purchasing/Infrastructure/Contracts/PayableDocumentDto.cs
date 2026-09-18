namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A document still open in the supplier's current account: an invoice with money owed, or a credit
/// note with credit left to use.
/// </summary>
/// <param name="DocumentId">The document a payment line refers to, together with its kind.</param>
/// <param name="IsCredit">
/// True for a credit note. <paramref name="OutstandingAmount"/> is then credit available, which a
/// payment takes off what it sends.
/// </param>
/// <param name="PaidAmount">Already settled by payments, or for a credit note already used.</param>
public sealed record PayableDocumentDto(
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
