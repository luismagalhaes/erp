namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// What the Inventory module needs to know about a product to value it and classify it. The
/// product file belongs to Core, so the caller supplies this.
/// </summary>
/// <param name="Category">
/// M merchandise, P finished product, A raw material, S by-product, T work in progress.
/// </param>
public sealed record InventoryFileProductDto(
    string ProductCode,
    string Description,
    string UnitOfMeasure,
    decimal UnitCost,
    string? Barcode = null,
    string Category = "M");
