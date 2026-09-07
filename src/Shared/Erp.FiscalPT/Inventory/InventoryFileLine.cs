namespace Erp.FiscalPT.Inventory;

/// <summary>
/// One product held at the reference date, in the field order the schemas fix: ProductCategory,
/// ProductCode, ProductDescription, ProductNumberCode, ClosingStockQuantity, UnitOfMeasure, and —
/// in the valued file only — ClosingStockValue.
/// </summary>
public sealed class InventoryFileLine
{
    /// <summary>
    /// M merchandise, P raw and consumable materials, A finished and intermediate products,
    /// S by-products and waste, T work in progress, B biological assets (valued file only).
    /// </summary>
    public string ProductCategory { get; init; } = InventoryConstants.DefaultProductCategory;

    public string ProductCode { get; init; } = string.Empty;

    public string ProductDescription { get; init; } = string.Empty;

    /// <summary>Barcode, or the product code again when there is none.</summary>
    public string ProductNumberCode { get; init; } = string.Empty;

    /// <summary>
    /// The quantity held. Both schemas refuse anything below zero, so negative stock cannot be
    /// reported — it has to be sorted out before the file is submitted.
    /// </summary>
    public decimal ClosingStockQuantity { get; init; }

    public string UnitOfMeasure { get; init; } = "UN";

    /// <summary>
    /// What the quantity is worth. Written only in the valued file; the quantities-only schema has
    /// no such element and would reject it.
    /// </summary>
    public decimal ClosingStockValue { get; init; }
}
