namespace Erp.FiscalPT.Documents;

/// <summary>
/// AT's "classeDoc" — the two letter document class the series webservice groups document types
/// under (SI = faturação, MG = documentos de transporte, PY = recibos). Not the same axis as
/// <see cref="FiscalConstants.SalesDocumentTypes"/> etc., which are the document types themselves.
/// </summary>
public static class SeriesDocumentClasses
{
    public const string Invoicing = "SI";
    public const string Transport = "MG";
    public const string Payments = "PY";

    public static string For(string documentType) => documentType switch
    {
        _ when SalesDocumentTypes.IsSupported(documentType) => Invoicing,
        _ when MovementDocumentTypes.IsSupported(documentType) => Transport,
        _ when PaymentDocumentTypes.IsSupported(documentType) => Payments,
        _ => throw new ArgumentException($"Unknown document type '{documentType}'.", nameof(documentType))
    };
}
