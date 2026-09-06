namespace Erp.FiscalPT.Documents;

/// <summary>SAF-T (PT) document types issued by the sales module.</summary>
public static class SalesDocumentTypes
{
    public const string Invoice = "FT";
    public const string SimplifiedInvoice = "FS";
    public const string InvoiceReceipt = "FR";
    public const string DebitNote = "ND";
    public const string CreditNote = "NC";

    public static readonly string[] All = [Invoice, SimplifiedInvoice, InvoiceReceipt, DebitNote, CreditNote];

    public static bool IsSupported(string documentType) =>
        All.Contains(documentType, StringComparer.Ordinal);
}

/// <summary>
/// SAF-T (PT) MovementType values, exported under MovementOfGoods. These are the documents
/// covered by the goods in circulation regime, which must reach the tax authority before the
/// transport starts.
/// </summary>
public static class MovementDocumentTypes
{
    /// <summary>Guia de remessa.</summary>
    public const string DeliveryNote = "GR";

    /// <summary>Guia de transporte.</summary>
    public const string TransportNote = "GT";

    /// <summary>Guia de movimentação de ativos próprios.</summary>
    public const string OwnAssetsNote = "GA";

    /// <summary>Guia de consignação.</summary>
    public const string ConsignmentNote = "GC";

    /// <summary>Guia ou nota de devolução.</summary>
    public const string ReturnNote = "GD";

    public static readonly string[] All = [DeliveryNote, TransportNote, OwnAssetsNote, ConsignmentNote, ReturnNote];

    public static bool IsSupported(string movementType) =>
        All.Contains(movementType, StringComparer.Ordinal);
}

/// <summary>SAF-T (PT) InvoiceStatus and MovementStatus values.</summary>
public static class DocumentStatuses
{
    public const string Normal = "N";
    public const string Voided = "A";
    public const string Billed = "F";
    public const string SelfBilled = "S";
    public const string Summary = "R";
}

/// <summary>SAF-T (PT) SourceBilling values.</summary>
public static class SourceBillingTypes
{
    public const string Produced = "P";
    public const string Integrated = "I";
    public const string Manual = "M";
}

/// <summary>Fiscal spaces with their own VAT rates.</summary>
public static class TaxCountryRegions
{
    public const string Mainland = "PT";
    public const string Azores = "PT-AC";
    public const string Madeira = "PT-MA";

    public static readonly string[] All = [Mainland, Azores, Madeira];
}

/// <summary>SAF-T (PT) TaxCode values for VAT.</summary>
public static class TaxCodes
{
    public const string Reduced = "RED";
    public const string Intermediate = "INT";
    public const string Normal = "NOR";
    public const string Exempt = "ISE";

    public static readonly string[] All = [Reduced, Intermediate, Normal, Exempt];
}
