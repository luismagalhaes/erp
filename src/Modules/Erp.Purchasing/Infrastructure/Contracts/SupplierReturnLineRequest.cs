namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="ReceiptLineId">
/// The receipt line the goods came in on. Required: we can only send back what we received.
/// </param>
public sealed record SupplierReturnLineRequest(Guid ReceiptLineId, decimal Quantity);
