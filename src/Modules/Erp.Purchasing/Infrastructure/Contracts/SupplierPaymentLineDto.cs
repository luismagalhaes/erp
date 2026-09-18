namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="AppliedAmount">Always positive; <paramref name="IsCredit"/> says it is taken off.</param>
public sealed record SupplierPaymentLineDto(
    int LineNumber,
    string DocumentKind,
    Guid DocumentId,
    string DocumentType,
    string DocumentNumber,
    DateOnly DocumentDate,
    bool IsCredit,
    decimal AppliedAmount);
