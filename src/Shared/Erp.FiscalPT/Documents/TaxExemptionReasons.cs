namespace Erp.FiscalPT.Documents;

/// <summary>
/// The tax authority's table of VAT exemption reasons ("Códigos de Motivo de Isenção"), which every
/// line taxed at zero has to cite in SAF-T TaxExemptionCode and TaxExemptionReason.
/// </summary>
/// <remarks>
/// Transcribed from the table published by the AT for e-Fatura and SAF-T (PT), including the codes
/// added since 2023 (M44, M45, M46). The table changes with the law: when the AT publishes a new
/// version, this list is what has to follow it. The legal basis is the applicable provision, which
/// is what SAF-T TaxExemptionReason asks for; the description is the mention the table pairs it with.
/// </remarks>
public static class TaxExemptionReasons
{
    /// <summary>SAF-T SAFPTPortugueseTaxExemptionReason is limited to this many characters.</summary>
    public const int MaxReasonLength = 60;

    public static readonly IReadOnlyList<TaxExemptionReason> All =
    [
        new("M01", "Artigo 16.º, n.º 6 do CIVA", "Artigo 16.º, n.º 6, alíneas a) a d) do CIVA"),
        new("M02", "Artigo 6.º do Decreto-Lei n.º 198/90, de 19 de junho", "Isenção do Decreto-Lei n.º 198/90"),
        new("M04", "Artigo 13.º do CIVA", "Isento — importações"),
        new("M05", "Artigo 14.º do CIVA", "Isento — exportações e operações assimiladas"),
        new("M06", "Artigo 15.º do CIVA", "Isento — regimes aduaneiros e entrepostos"),
        new("M07", "Artigo 9.º do CIVA", "Isento — operações internas (saúde, ensino, locação...)"),
        new("M09", "Artigo 62.º alínea b) do CIVA", "IVA - não confere direito a dedução"),
        new("M10", "Artigo 57.º do CIVA", "IVA - regime de isenção"),
        new("M11", "Decreto-Lei n.º 346/85, de 23 de agosto", "Regime particular do tabaco"),
        new("M12", "Decreto-Lei n.º 221/85, de 3 de julho", "Regime da margem de lucro - Agências de viagens"),
        new("M13", "Decreto-Lei n.º 199/96, de 18 de outubro", "Regime da margem de lucro - Bens em segunda mão"),
        new("M14", "Decreto-Lei n.º 199/96, de 18 de outubro", "Regime da margem de lucro - Objetos de arte"),
        new("M15", "Decreto-Lei n.º 199/96, de 18 de outubro", "Regime da margem de lucro - Objetos de coleção e antiguidades"),
        new("M16", "Artigo 14.º do RITI", "Isento — transmissões intracomunitárias de bens"),
        new("M19", "Isenções temporárias determinadas em diploma próprio", "Outras isenções"),
        new("M20", "Artigo 59.º-D n.º 2 do CIVA", "IVA - regime forfetário"),
        new("M21", "Artigo 72.º n.º 4 do CIVA", "IVA - não confere direito à dedução (ou expressão similar)"),
        new("M25", "Artigo 38.º n.º 1 alínea a) do CIVA", "Mercadorias à consignação"),
        new("M26", "Lei n.º 17/2023, de 14 de abril", "Isenção de IVA com direito à dedução no cabaz alimentar"),
        new("M30", "Artigo 2.º n.º 1 alínea i) do CIVA", "IVA - autoliquidação"),
        new("M31", "Artigo 2.º n.º 1 alínea j) do CIVA", "IVA - autoliquidação"),
        new("M32", "Artigo 2.º n.º 1 alínea l) do CIVA", "IVA - autoliquidação"),
        new("M33", "Artigo 2.º n.º 1 alínea m) do CIVA", "IVA - autoliquidação"),
        new("M34", "Artigo 2.º n.º 1 alínea n) do CIVA", "IVA - autoliquidação"),
        new("M40", "Artigo 6.º n.º 6 alínea a) do CIVA, a contrário", "IVA - autoliquidação"),
        new("M41", "Artigo 8.º n.º 3 do RITI", "IVA - autoliquidação"),
        new("M42", "Decreto-Lei n.º 21/2007, de 29 de janeiro", "IVA - autoliquidação"),
        new("M43", "Decreto-Lei n.º 362/99, de 16 de setembro", "IVA - autoliquidação"),
        new("M44", "Artigo 6.º do CIVA", "IVA - regras específicas - artigo 6.º"),
        new("M45", "Artigo 58.º-A do CIVA", "IVA - regime transfronteiriço de isenção"),
        new("M46", "Decreto-Lei n.º 19/2017, de 14 de fevereiro", "e-TaxFree"),
        new("M99", "Não sujeito ou não tributado", "Outras situações de não liquidação do imposto")
    ];

    public static TaxExemptionReason? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(reason => string.Equals(reason.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The code and reason a line is written with. An exempt line has to name a reason from the
    /// table, and its reason defaults to the table's legal basis; a line that is not exempt carries
    /// neither, whatever was sent.
    /// </summary>
    /// <returns>The pair to store, or an error completing "Line N ..." when the line is refused.</returns>
    public static (string? Code, string? Reason, string? Error) Resolve(string taxCode, string? code, string? reason)
    {
        if (!string.Equals(taxCode, TaxCodes.Exempt, StringComparison.Ordinal))
            return (null, null, null);

        var entry = Find(code);

        if (entry is null)
        {
            return (null, null, string.IsNullOrWhiteSpace(code)
                ? "is exempt and requires a VAT exemption code from the tax authority's table (M01 to M99)."
                : $"has an unknown VAT exemption code '{code}'.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? entry.LegalBasis : reason.Trim();

        return text.Length > MaxReasonLength
            ? (null, null, $"has an exemption reason longer than the {MaxReasonLength} characters SAF-T allows.")
            : (entry.Code, text, null);
    }
}
