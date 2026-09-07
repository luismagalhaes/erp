namespace Erp.FiscalPT.Saft;

/// <summary>The tax applied to a line. Points at an entry of the TaxTable.</summary>
public sealed class SaftTax
{
    public string TaxType { get; init; } = SaftConstants.TaxTypeVat;

    public string TaxCountryRegion { get; init; } = SaftConstants.CountryDefault;

    public string TaxCode { get; init; } = string.Empty;

    public decimal TaxPercentage { get; init; }
}
