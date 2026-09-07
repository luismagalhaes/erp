namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// The outcome of checking stock: the balances on record against the balances recomputed from the
/// ledger. A difference means something wrote outside the normal path, which is exactly what the
/// check exists to surface.
/// </summary>
/// <param name="CheckedAtUtc">When the check ran; the ledger may have moved on since.</param>
public sealed record StockCheckResultDto(
    Guid CompanyId,
    DateTime CheckedAtUtc,
    int ProductsChecked,
    int ProductsWithDifference,
    IReadOnlyList<StockCheckLineDto> Lines);
