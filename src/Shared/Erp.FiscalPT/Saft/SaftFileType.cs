namespace Erp.FiscalPT.Saft;

/// <summary>
/// What kind of SAF-T file this is — the <c>TaxAccountingBasis</c> of the header. Not a variant of
/// one file: each value is a **different file**, with its own header and its own documents.
/// </summary>
/// <remarks>
/// The schema lists more (<c>C</c> accounting, <c>I</c> integrated, <c>P</c> partial invoicing,
/// <c>R</c> receipts only, <c>T</c> transport only). Only what we actually produce is here, so an
/// unimplemented value cannot be selected by accident.
/// </remarks>
public static class SaftFileType
{
    /// <summary>Faturação: what we issued in our own name.</summary>
    public const string Billing = "F";

    /// <summary>
    /// Autofaturação: documents we issued **on a supplier's behalf**. A separate file, one per
    /// supplier, with the supplier's tax id in the header — the invoices are their sales.
    /// </summary>
    public const string SelfBilling = "S";

    public static readonly string[] All = [Billing, SelfBilling];

    public static bool IsSupported(string fileType) => All.Contains(fileType, StringComparer.Ordinal);
}
