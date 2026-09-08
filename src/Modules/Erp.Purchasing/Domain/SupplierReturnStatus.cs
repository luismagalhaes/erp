namespace Erp.Purchasing.Domain;

public enum SupplierReturnStatus : byte
{
    /// <summary>The goods went back and the stock left the warehouse.</summary>
    Returned = 0,

    /// <summary>Undone. The stock came back in and the receipt is owed the quantity again.</summary>
    Voided = 1
}
