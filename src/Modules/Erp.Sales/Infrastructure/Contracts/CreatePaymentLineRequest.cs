namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="OriginatingDocumentId">Invoice being settled.</param>
/// <param name="AppliedAmount">How much of that invoice this receipt settles.</param>
public sealed record CreatePaymentLineRequest(Guid OriginatingDocumentId, decimal AppliedAmount);
