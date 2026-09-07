namespace Erp.FiscalPT.Inventory;

/// <summary>
/// Fixed values of the inventory communication file, taken from the official schemas published by
/// the tax authority: <c>Stock_1_2.xsd</c> and <c>Inventario_2_01.xsd</c>.
/// </summary>
public static class InventoryConstants
{
    /// <summary>First year for which the valued file is mandatory.</summary>
    public const int FirstValuedFiscalYear = 2021;

    /// <summary>Merchandise. The default when a product says nothing about its category.</summary>
    public const string DefaultProductCategory = "M";

    /// <summary>Target namespace of each schema.</summary>
    public static string Namespace(InventoryFileVersion version) => version switch
    {
        InventoryFileVersion.QuantitiesOnly => "urn:StockFile:PT_1_02",
        InventoryFileVersion.Valued => "urn:StockFile:PT_2_01",
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    /// <summary>The only value each schema's FileVersion pattern accepts.</summary>
    public static string FileVersion(InventoryFileVersion version) => version switch
    {
        InventoryFileVersion.QuantitiesOnly => "1_02",
        InventoryFileVersion.Valued => "2_01",
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    /// <summary>Only the valued file carries ClosingStockValue.</summary>
    public static bool CarriesValue(InventoryFileVersion version) =>
        version == InventoryFileVersion.Valued;

    /// <summary>
    /// Categories each schema accepts. The valued one adds B, biological assets, which the older
    /// one would reject.
    /// </summary>
    public static string[] ProductCategories(InventoryFileVersion version) => version switch
    {
        InventoryFileVersion.QuantitiesOnly => ["M", "P", "A", "S", "T"],
        InventoryFileVersion.Valued => ["M", "P", "A", "S", "T", "B"],
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public static bool IsKnownCategory(InventoryFileVersion version, string category) =>
        ProductCategories(version).Contains(category, StringComparer.Ordinal);
}
