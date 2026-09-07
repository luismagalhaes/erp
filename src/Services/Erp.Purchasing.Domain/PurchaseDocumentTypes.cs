namespace Erp.Purchasing.Domain;

/// <summary>
/// The kinds of document a supplier sends us. These are <b>their</b> document types, recorded as
/// they came — we do not issue any of them, so nothing here is numbered or signed by us.
/// </summary>
public static class PurchaseDocumentTypes
{
    /// <summary>Fatura.</summary>
    public const string Invoice = "FT";

    /// <summary>Fatura simplificada.</summary>
    public const string SimplifiedInvoice = "FS";

    /// <summary>Fatura-recibo.</summary>
    public const string InvoiceReceipt = "FR";

    /// <summary>Nota de crédito. Takes back what was invoiced, so it carries negative quantities.</summary>
    public const string CreditNote = "NC";

    /// <summary>Nota de débito.</summary>
    public const string DebitNote = "ND";

    public static readonly string[] All = [Invoice, SimplifiedInvoice, InvoiceReceipt, CreditNote, DebitNote];

    public static bool IsSupported(string documentType) =>
        All.Contains(documentType, StringComparer.Ordinal);

    /// <summary>
    /// True for the documents that give value back rather than charge it. A credit note from a
    /// supplier reduces what we owe and reverses the input VAT we deducted.
    /// </summary>
    public static bool IsCredit(string documentType) =>
        string.Equals(documentType, CreditNote, StringComparison.Ordinal);
}
