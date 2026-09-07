namespace Erp.FiscalPT.Saft;

/// <summary>
/// A rate in the TaxTable. Every combination of country region and tax code used in the
/// documents has to be declared here.
/// </summary>
public sealed class SaftTaxTableEntry
{
    public string TaxType { get; init; } = SaftConstants.TaxTypeVat;

    public string TaxCountryRegion { get; init; } = SaftConstants.CountryDefault;

    public string TaxCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Rate as a percentage. Exempt lines carry zero.</summary>
    public decimal TaxPercentage { get; init; }
}
