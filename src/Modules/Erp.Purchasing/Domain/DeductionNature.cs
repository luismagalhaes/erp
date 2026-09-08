namespace Erp.Purchasing.Domain;

/// <summary>
/// What the purchase was for. The periodic VAT return separates deductible input tax by nature, so
/// it is recorded per line and not per document — the same invoice brings goods and services.
/// </summary>
public enum DeductionNature : byte
{
    /// <summary>Existências: goods bought to hold or resell. The only nature that moves stock.</summary>
    Inventory = 0,

    /// <summary>Imobilizado.</summary>
    FixedAssets = 1,

    /// <summary>Outros bens e serviços.</summary>
    OtherGoodsAndServices = 2
}
