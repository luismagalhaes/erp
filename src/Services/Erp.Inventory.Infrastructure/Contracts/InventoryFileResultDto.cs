namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="TotalValue">
/// What the stock is worth. It reaches the file only on the valued version (2_01); on the older
/// quantities-only one (1_02) it is computed anyway, so the figure is at hand.
/// </param>
/// <param name="ProductsWithoutCost">
/// How many reported products have no cost on file, and so are worth nothing in the valuation.
/// Only counted on the valued file, the one that has to carry a figure.
/// </param>
/// <param name="ProductsWithNegativeStock">
/// How many products hold less than nothing. The schema refuses these, so the file will not
/// validate until the stock is sorted out.
/// </param>
/// <param name="ValidationErrors">
/// What the official schema rejected, empty when the file conforms.
/// </param>
public sealed record InventoryFileResultDto(
    string FileName,
    byte[] Content,
    int LineCount,
    decimal TotalQuantity,
    decimal TotalValue,
    int ProductsWithoutCost,
    int ProductsWithNegativeStock,
    IReadOnlyList<string> ValidationErrors);
