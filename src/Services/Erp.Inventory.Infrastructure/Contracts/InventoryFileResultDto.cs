namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="TotalValue">
/// What the stock is worth. Not written to the file: schema 1_02 carries quantities only, and the
/// valuation Portaria 126/2019 added belongs to a later version. Shown so the figure is at hand.
/// </param>
/// <param name="ProductsWithoutCost">
/// How many reported products have no cost on file, and so are worth nothing in the valuation.
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
