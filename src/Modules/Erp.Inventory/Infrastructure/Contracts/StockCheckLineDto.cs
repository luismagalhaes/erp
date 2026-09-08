namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// One product in one warehouse, with the figures on record against the same figures recomputed
/// from the ledger. They should always agree.
/// </summary>
/// <param name="Difference">Recorded minus recomputed. Zero is what a healthy product looks like.</param>
/// <param name="ValueDifference">
/// The same comparison for the value. It is checked separately because the average cost depends on
/// the <b>order</b> the movements arrived in, not merely their sum — so a value can drift while the
/// quantity still agrees.
/// </param>
public sealed record StockCheckLineDto(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal RecordedQuantity,
    decimal LedgerQuantity,
    decimal Difference,
    decimal RecordedValue,
    decimal LedgerValue,
    decimal ValueDifference,
    int EntryCount);
