namespace Erp.Sales.Infrastructure.Contracts;

public sealed record PaymentListItemDto(
    Guid Id,
    string PaymentRefNo,
    string PaymentType,
    string Atcud,
    DateOnly TransactionDate,
    string PartyName,
    string PartyTaxId,
    decimal GrossTotal,
    int SettledInvoiceCount,
    string Status);
