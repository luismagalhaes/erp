namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// One product in one warehouse, with the balance on record against the balance recomputed from
/// the ledger. They should always agree.
/// </summary>
/// <param name="Difference">Recorded minus recomputed. Zero is what a healthy product looks like.</param>
public sealed record StockCheckLineDto(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal RecordedQuantity,
    decimal LedgerQuantity,
    decimal Difference,
    int EntryCount);
