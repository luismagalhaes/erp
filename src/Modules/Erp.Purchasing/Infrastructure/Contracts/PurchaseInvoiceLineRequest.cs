namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="ReceiptLineId">
/// The receipt line this invoices. When it is set the goods already came in on the receipt, and
/// this line will not bring them in a second time.
/// </param>
/// <param name="ReturnLineId">
/// On a credit note, the return line being credited. Traceability only: the stock already left on
/// the return, and the credit note is about the money.
/// </param>
/// <param name="DeductionNature">
/// Inventory, FixedAssets or OtherGoodsAndServices. Only Inventory moves stock.
/// </param>
public sealed record PurchaseInvoiceLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string TaxCode = "NOR",
    decimal TaxPercentage = 23m,
    string DeductionNature = "Inventory",
    Guid? ReceiptLineId = null,
    Guid? ReturnLineId = null);
