using Erp.FiscalPT.Documents;

namespace Erp.FiscalPT.Saft;

/// <summary>The description the TaxTable carries for each rate.</summary>
public static class SaftTaxDescriptions
{
    public static string For(string taxCode) => taxCode switch
    {
        TaxCodes.Normal => "Taxa normal",
        TaxCodes.Intermediate => "Taxa intermédia",
        TaxCodes.Reduced => "Taxa reduzida",
        TaxCodes.Exempt => "Isento",
        // An unknown code is described by itself rather than by a guess: the file still validates,
        // and whoever reads it sees exactly what was in the document.
        _ => taxCode
    };
}
