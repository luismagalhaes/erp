using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// What a client sends to place an order. It names the supplier; the host reads the supplier file
/// from Core and copies it onto the order, so a caller cannot pass a name and a tax id that were
/// never on file.
/// </summary>
public sealed record CreatePurchaseOrderApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly OrderDate,
    Guid WarehouseId,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    DateOnly? ExpectedDate = null,
    string? Notes = null,
    bool Place = false);
