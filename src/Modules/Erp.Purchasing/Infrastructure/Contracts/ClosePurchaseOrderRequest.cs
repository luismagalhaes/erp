namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>Used both to close an order that will not be completed and to cancel one.</summary>
public sealed record ClosePurchaseOrderRequest(string Reason);
