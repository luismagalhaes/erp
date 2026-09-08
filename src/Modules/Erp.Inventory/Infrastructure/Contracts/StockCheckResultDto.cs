namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// The outcome of checking stock: the balances on record against the balances recomputed from the
/// ledger. A difference means something wrote outside the normal path, which is exactly what the
/// check exists to surface.
/// </summary>
/// <param name="CheckedAtUtc">When the check ran; the ledger may have moved on since.</param>
/// <param name="ProductsWithValueDifference">
/// Counted apart from the quantity differences, because they fail for different reasons: a quantity
/// drifts when something wrote outside the normal path, a value drifts when movements were applied
/// in a different order than the ledger records.
/// </param>
public sealed record StockCheckResultDto(
    Guid CompanyId,
    DateTime CheckedAtUtc,
    int ProductsChecked,
    int ProductsWithDifference,
    int ProductsWithValueDifference,
    IReadOnlyList<StockCheckLineDto> Lines);
