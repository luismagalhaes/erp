namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="AtDocCodeId">Code the tax authority returns when the document is communicated.</param>
public sealed record CommunicateStockMovementRequest(string AtDocCodeId);
