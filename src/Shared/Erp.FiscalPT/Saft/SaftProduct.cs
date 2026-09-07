namespace Erp.FiscalPT.Saft;

/// <summary>A product or service in MasterFiles.</summary>
public sealed class SaftProduct
{
    /// <summary>P for goods, S for services, O for others, I for taxes.</summary>
    public string ProductType { get; init; } = SaftConstants.ProductTypeGoods;

    public string ProductCode { get; init; } = string.Empty;

    public string ProductDescription { get; init; } = string.Empty;

    /// <summary>Barcode or, when there is none, the product code again.</summary>
    public string ProductNumberCode { get; init; } = string.Empty;
}
