using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// What a client sends to record a supplier's invoice. It names the supplier; the host reads the
/// supplier file from Core and copies it onto the record.
/// </summary>
public sealed record RecordPurchaseInvoiceApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    IReadOnlyList<PurchaseInvoiceLineRequest> Lines,
    Guid? WarehouseId = null,
    string? SupplierAtcud = null,
    DateOnly? DueDate = null,
    bool ReverseCharge = false,
    string? Notes = null);
