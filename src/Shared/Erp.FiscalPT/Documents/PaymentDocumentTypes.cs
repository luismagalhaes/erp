namespace Erp.FiscalPT.Documents;

/// <summary>
/// SAF-T (PT) PaymentType values, exported under Payments. Receipts are fiscally relevant
/// documents: they are numbered, signed and communicated like an invoice.
/// </summary>
public static class PaymentDocumentTypes
{
    /// <summary>Recibo emitido no âmbito do regime de IVA de caixa.</summary>
    public const string CashVatReceipt = "RC";

    /// <summary>Outros recibos.</summary>
    public const string OtherReceipt = "RG";

    public static readonly string[] All = [CashVatReceipt, OtherReceipt];

    public static bool IsSupported(string paymentType) =>
        All.Contains(paymentType, StringComparer.Ordinal);
}
