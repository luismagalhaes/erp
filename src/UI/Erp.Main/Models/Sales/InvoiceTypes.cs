namespace Erp.Main.Models.Sales;

/// <summary>
/// Invoicing document types with the designation the printed document has to spell out in full.
/// </summary>
public static class InvoiceTypes
{
    public static readonly (string Code, string Label)[] All =
    [
        ("FT", "Fatura"),
        ("FS", "Fatura simplificada"),
        ("FR", "Fatura-recibo"),
        ("NC", "Nota de crédito"),
        ("ND", "Nota de débito")
    ];

    public const string InvoiceReceipt = "FR";

    /// <summary>
    /// Types that correct another document, and so must name it and say why — artigo 36.º n.º 5
    /// do CIVA. The API enforces the same rule.
    /// </summary>
    public static readonly string[] Rectifying = ["NC", "ND"];

    /// <summary>Types that create a sale, as opposed to correcting one.</summary>
    public static readonly string[] Sale = ["FT", "FS", "FR"];

    public static bool IsRectifying(string code) =>
        Rectifying.Contains(code, StringComparer.Ordinal);

    public static bool IsSale(string code) =>
        Sale.Contains(code, StringComparer.Ordinal);

    /// <summary>Paid when issued, so the document itself records how.</summary>
    public static bool IsInvoiceReceipt(string code) => code == InvoiceReceipt;

    public static string Describe(string code) =>
        All.FirstOrDefault(type => type.Code == code).Label ?? code;
}
