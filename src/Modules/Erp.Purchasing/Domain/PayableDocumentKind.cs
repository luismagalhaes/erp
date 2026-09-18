namespace Erp.Purchasing.Domain;

/// <summary>
/// Where a document we owe money on lives. Two tables hold them, so a payment line says which one
/// it points at.
/// </summary>
public enum PayableDocumentKind : byte
{
    /// <summary>A supplier's invoice, recorded in our books.</summary>
    PurchaseInvoice = 0,

    /// <summary>An invoice we issued in the supplier's name.</summary>
    SelfBilledInvoice = 1
}
