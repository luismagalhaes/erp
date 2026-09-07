namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// What the Inventory module needs to know about a product to value it and classify it. The
/// product file belongs to Core, so the caller supplies this.
/// </summary>
/// <param name="Category">
/// M merchandise, P finished product, A raw material, S by-product, T work in progress, and B
/// biological asset on the valued schema only. A category the chosen version does not know falls
/// back to merchandise rather than producing a file the tax authority would reject.
/// </param>
public sealed record InventoryFileProductDto(
    string ProductCode,
    string Description,
    string UnitOfMeasure,
    decimal UnitCost,
    string? Barcode = null,
    string Category = "M");
