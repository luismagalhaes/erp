using Erp.Inventory.Domain;

namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="Direction">
/// Which way the document moves stock, taken from the series that issued it.
/// </param>
public sealed record RecordDocumentStockRequest(
    Guid CompanyId,
    Guid WarehouseId,
    StockDirection Direction,
    DateOnly MovementDate,
    string DocumentType,
    string DocumentNumber,
    Guid DocumentId,
    IReadOnlyList<DocumentStockLine> Lines);
