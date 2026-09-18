namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="DocumentKind">PurchaseInvoice or SelfBilledInvoice: which table the document lives in.</param>
/// <param name="DocumentId">Document being settled.</param>
/// <param name="AppliedAmount">
/// How much of that document this payment settles — or, for a credit note, how much of its credit
/// it uses. Always positive.
/// </param>
public sealed record SupplierPaymentLineRequest(
    string DocumentKind,
    Guid DocumentId,
    decimal AppliedAmount);
