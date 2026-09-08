using Erp.FiscalPT.Documents;

namespace Erp.SeriesRegistry.Domain;

/// <summary>
/// What each document type does to stock unless the user says otherwise when creating the series.
/// </summary>
public static class DefaultStockEffects
{
    public static StockEffect For(string documentType) => documentType switch
    {
        // Goods leave on a delivery, a transport note or a consignment.
        MovementDocumentTypes.DeliveryNote or
        MovementDocumentTypes.TransportNote or
        MovementDocumentTypes.ConsignmentNote => StockEffect.Out,

        // A return brings them back.
        MovementDocumentTypes.ReturnNote => StockEffect.In,

        // Own assets move between the entity's own places; nothing is bought or sold.
        MovementDocumentTypes.OwnAssetsNote => StockEffect.None,

        // A sale with no delivery note before it takes the goods out itself.
        SalesDocumentTypes.Invoice or
        SalesDocumentTypes.SimplifiedInvoice or
        SalesDocumentTypes.InvoiceReceipt => StockEffect.Out,

        // A credit note usually follows goods coming back.
        SalesDocumentTypes.CreditNote => StockEffect.In,

        // A debit note corrects value, never quantity. Receipts move money, not goods.
        _ => StockEffect.None
    };
}
