namespace Erp.Purchasing.Domain;

public enum PurchaseInvoiceStatus : byte
{
    /// <summary>Recorded. The VAT on it is deductible and it counts towards what is owed.</summary>
    Recorded = 0,

    /// <summary>
    /// Struck out. Recording it was a mistake of ours — the supplier's document is unaffected,
    /// because it was never ours to cancel.
    /// </summary>
    Voided = 1
}
