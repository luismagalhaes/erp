namespace Erp.Sales.Infrastructure.Contracts;

public sealed record PaymentDetailDto(
    Guid Id,
    string PaymentRefNo,
    string PaymentType,
    string Atcud,
    DateOnly TransactionDate,
    DateTime SystemEntryDateUtc,
    string Status,
    string PartyName,
    string PartyTaxId,
    string? Description,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string PrintableHash,
    string QrCodePayload,
    IReadOnlyList<PaymentLineDto> Lines,
    IReadOnlyList<PaymentMethodDto> Methods);
