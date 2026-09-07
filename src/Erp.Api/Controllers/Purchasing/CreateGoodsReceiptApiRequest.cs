using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// What a client sends to record goods arriving. It names the supplier; the host reads the supplier
/// file from Core and copies it onto the receipt.
/// </summary>
public sealed record CreateGoodsReceiptApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly ReceiptDate,
    Guid WarehouseId,
    IReadOnlyList<GoodsReceiptLineRequest> Lines,
    string? SupplierDocumentNumber = null,
    DateOnly? SupplierDocumentDate = null,
    string? Notes = null);
