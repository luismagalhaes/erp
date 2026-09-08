namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="DocumentLineId">The line moving the stock. Recorded, so it can only move once.</param>
/// <param name="OriginatingLineId">
/// The line this one came from, when the document integrates another. If that line already moved
/// the stock, this one does not move it again.
/// </param>
public sealed record DocumentStockLine(
    Guid DocumentLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    Guid? OriginatingLineId = null,
    decimal? UnitCost = null);
