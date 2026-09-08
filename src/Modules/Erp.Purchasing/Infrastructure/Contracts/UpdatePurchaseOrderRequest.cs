namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record UpdatePurchaseOrderRequest(
    DateOnly OrderDate,
    Guid WarehouseId,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    DateOnly? ExpectedDate = null,
    string? Notes = null);
