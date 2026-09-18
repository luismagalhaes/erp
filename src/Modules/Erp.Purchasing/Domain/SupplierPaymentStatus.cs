namespace Erp.Purchasing.Domain;

public enum SupplierPaymentStatus : byte
{
    /// <summary>Recorded. The documents it settles are paid by what it says.</summary>
    Recorded = 0,

    /// <summary>Struck out. The documents it settled go back to being owed.</summary>
    Voided = 1
}
