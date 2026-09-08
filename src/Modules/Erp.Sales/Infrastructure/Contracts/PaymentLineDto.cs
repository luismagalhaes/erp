namespace Erp.Sales.Infrastructure.Contracts;

public sealed record PaymentLineDto(
    int LineNumber,
    Guid OriginatingDocumentId,
    string OriginatingNumber,
    DateOnly OriginatingDate,
    decimal AppliedAmount);
