namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="PartyTaxId">Leave empty for an unidentified final consumer.</param>
public sealed record CreatePaymentRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly TransactionDate,
    string? PartyTaxId,
    string PartyName,
    IReadOnlyList<CreatePaymentLineRequest> Lines,
    IReadOnlyList<CreatePaymentMethodRequest> Methods,
    string? Description = null);
