namespace Erp.Main.Models.Purchasing;

/// <summary>The two tables a payable document may live in, as the API names them.</summary>
public static class PayableDocumentKinds
{
    public const string PurchaseInvoice = "PurchaseInvoice";

    public const string SelfBilledInvoice = "SelfBilledInvoice";

    /// <summary>Where the document opens in this app.</summary>
    public static string ViewRoute(string kind, Guid documentId) =>
        kind == SelfBilledInvoice
            ? $"self-billed-invoices/{documentId}"
            : $"supplier-invoices/{documentId}";
}
