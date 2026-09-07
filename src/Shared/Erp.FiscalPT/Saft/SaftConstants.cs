namespace Erp.FiscalPT.Saft;

/// <summary>
/// Fixed values of the SAF-T (PT) file, as set by Portaria 302/2016 and the amendments that
/// introduced the ATCUD (Portaria 195/2020).
/// </summary>
public static class SaftConstants
{
    /// <summary>Schema version this writer produces.</summary>
    public const string AuditFileVersion = "1.04_01";

    public const string Namespace = "urn:OECD:StandardAuditFile-Tax:PT_1.04_01";

    /// <summary>Only billing data is exported, so the accounting basis is "F".</summary>
    public const string TaxAccountingBasisBilling = "F";

    /// <summary>The file covers the whole taxable entity, not a single establishment.</summary>
    public const string TaxEntityGlobal = "Global";

    public const string CurrencyCode = "EUR";

    public const string CountryDefault = "PT";

    /// <summary>Placeholder the tax authority expects where a value is unknown.</summary>
    public const string Unknown = "Desconhecido";

    /// <summary>
    /// The schema caps SourceID at 30 characters, which is shorter than the 36 character GUIDs
    /// ASP.NET Identity hands out.
    /// </summary>
    public const int SourceIdMaxLength = 30;

    /// <summary>Only VAT is exported; stamp duty would be "IS".</summary>
    public const string TaxTypeVat = "IVA";

    /// <summary>SAF-T ProductType: goods.</summary>
    public const string ProductTypeGoods = "P";

    /// <summary>SAF-T ProductType: services.</summary>
    public const string ProductTypeService = "S";

    /// <summary>Document families whose lines are debits rather than credits.</summary>
    public static readonly string[] DebitDocumentTypes = ["NC"];

    /// <summary>True when the document's lines go to DebitAmount instead of CreditAmount.</summary>
    public static bool IsDebitDocument(string documentType) =>
        DebitDocumentTypes.Contains(documentType, StringComparer.Ordinal);
}
