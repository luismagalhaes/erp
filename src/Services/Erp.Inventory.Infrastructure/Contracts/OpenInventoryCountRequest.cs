namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="WarehouseId">Null counts every warehouse of the company.</param>
/// <param name="ProductCodes">
/// Null or empty counts everything in scope; a list narrows the count to those products.
/// </param>
/// <param name="StartAtZero">
/// True to open every line at zero. Closing such a count empties the stock in scope — the "zerar"
/// step before recounting from nothing.
/// </param>
public sealed record OpenInventoryCountRequest(
    Guid CompanyId,
    string Reference,
    DateOnly CountDate,
    Guid? WarehouseId = null,
    IReadOnlyList<string>? ProductCodes = null,
    bool StartAtZero = false);
