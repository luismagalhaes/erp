namespace Erp.Main.Models.Purchasing;

// --- Purchase orders ---

public sealed record PurchaseOrderListItem(
    Guid Id,
    string Number,
    string Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    string SupplierName,
    string SupplierTaxId,
    int LineCount,
    decimal GrossTotal,
    bool IsOpen);

public sealed record PurchaseOrderSupplier(
    string Code,
    string Name,
    string TaxId,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    string Country = "PT");

public sealed record PurchaseOrderLine(
    Guid Id,
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal ReceivedQuantity,
    decimal PendingQuantity,
    decimal DiscountPercentage = 0m);

public sealed record PurchaseOrder(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid WarehouseId,
    PurchaseOrderSupplier Supplier,
    string? Notes,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    string? ClosedReason,
    IReadOnlyList<PurchaseOrderLine> Lines)
{
    public bool IsDraft => Status == "Draft";

    public bool IsOpen => Status is "Placed" or "PartiallyReceived";

    public bool IsFinished => Status is "Closed" or "Cancelled";

    public bool HasReceipts => Lines.Any(line => line.ReceivedQuantity > 0);

    /// <summary>Lines may only be rewritten while nothing has arrived.</summary>
    public bool CanEditLines => !IsFinished && !HasReceipts;
}

public sealed record CreatePurchaseOrderRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly OrderDate,
    Guid WarehouseId,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    DateOnly? ExpectedDate = null,
    string? Notes = null,
    bool Place = false);

public sealed record UpdatePurchaseOrderRequest(
    DateOnly OrderDate,
    Guid WarehouseId,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    DateOnly? ExpectedDate = null,
    string? Notes = null);

public sealed record PurchaseOrderLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string TaxCode = "NOR",
    decimal TaxPercentage = 23m,
    decimal DiscountPercentage = 0m);

public sealed record ClosePurchaseOrderRequest(string Reason);

/// <summary>A line the supplier still owes. Feeds the goods receipt when phase 2 arrives.</summary>
public sealed record PendingOrderLine(
    Guid OrderId,
    string OrderNumber,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    Guid LineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal PendingQuantity,
    decimal UnitPrice,
    decimal DiscountPercentage = 0m);

// --- Goods receipts ---

public sealed record GoodsReceiptListItem(
    Guid Id,
    string Number,
    string Status,
    DateOnly ReceiptDate,
    string SupplierName,
    string? SupplierDocumentNumber,
    int LineCount,
    decimal TotalCost,
    bool IsVoided);

public sealed record GoodsReceiptLine(
    Guid Id,
    int LineNumber,
    Guid? OrderId,
    Guid? OrderLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitCost,
    decimal LineAmount,
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m);

public sealed record GoodsReceipt(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly ReceiptDate,
    Guid WarehouseId,
    string? SupplierDocumentNumber,
    DateOnly? SupplierDocumentDate,
    PurchaseOrderSupplier Supplier,
    string? Notes,
    decimal TotalCost,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<GoodsReceiptLine> Lines)
{
    public bool IsVoided => Status == "Voided";
}

/// <param name="OrderLineId">The order line being received, when there is an order behind it.</param>
public sealed record GoodsReceiptLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitCost,
    string UnitOfMeasure = "UN",
    Guid? OrderLineId = null,
    decimal DiscountPercentage = 0m);

public sealed record CreateGoodsReceiptRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly ReceiptDate,
    Guid WarehouseId,
    IReadOnlyList<GoodsReceiptLineRequest> Lines,
    string? SupplierDocumentNumber = null,
    DateOnly? SupplierDocumentDate = null,
    string? Notes = null);

public sealed record VoidGoodsReceiptRequest(string Reason);

// --- Supplier invoices ---

public sealed record PurchaseInvoiceListItem(
    Guid Id,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    DateOnly? DueDate,
    string SupplierName,
    string SupplierTaxId,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    bool ReverseCharge,
    string Status,
    bool IsVoided);

public sealed record PurchaseInvoiceLine(
    Guid Id,
    int LineNumber,
    Guid? ReceiptId,
    Guid? ReceiptLineId,
    Guid? OrderLineId,
    Guid? ReturnLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    string DeductionNature,
    bool MovesStock,
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m);

public sealed record PurchaseInvoiceTaxSummary(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record PurchaseInvoice(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    string? SupplierAtcud,
    DateOnly ReceivedDate,
    DateOnly? DueDate,
    bool ReverseCharge,
    string Status,
    Guid? WarehouseId,
    PurchaseOrderSupplier Supplier,
    string? Notes,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    bool MovedStock,
    IReadOnlyList<PurchaseInvoiceLine> Lines,
    IReadOnlyList<PurchaseInvoiceTaxSummary> TaxSummary)
{
    public bool IsVoided => Status == "Voided";

    /// <summary>Only a record that moved no stock can be corrected in place.</summary>
    public bool CanEdit => !IsVoided && !MovedStock;
}

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
    Guid? ReturnLineId = null,
    decimal DiscountPercentage = 0m);

public sealed record RecordPurchaseInvoiceRequest(
    Guid CompanyId,
    Guid SupplierId,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    IReadOnlyList<PurchaseInvoiceLineRequest> Lines,
    Guid? WarehouseId = null,
    string? SupplierAtcud = null,
    DateOnly? DueDate = null,
    bool ReverseCharge = false,
    string? Notes = null);

public sealed record VoidPurchaseInvoiceRequest(string Reason);

/// <summary>A receipt line that has not been invoiced yet.</summary>
public sealed record UninvoicedReceiptLine(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    string? SupplierDocumentNumber,
    Guid SupplierId,
    string SupplierName,
    Guid ReceiptLineId,
    Guid? OrderLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal InvoicedQuantity,
    decimal PendingQuantity,
    decimal UnitCost,
    decimal DiscountPercentage = 0m);

// --- Returns to supplier ---

public sealed record SupplierReturnListItem(
    Guid Id,
    string Number,
    string Status,
    DateOnly ReturnDate,
    string SupplierName,
    string Reason,
    int LineCount,
    decimal TotalCost,
    bool IsVoided);

public sealed record SupplierReturnLine(
    Guid Id,
    int LineNumber,
    Guid ReceiptId,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitCost,
    decimal LineAmount);

public sealed record SupplierReturn(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    string Status,
    DateOnly ReturnDate,
    Guid WarehouseId,
    string Reason,
    PurchaseOrderSupplier Supplier,
    string? Notes,
    decimal TotalCost,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<SupplierReturnLine> Lines)
{
    public bool IsVoided => Status == "Voided";
}

public sealed record SupplierReturnLineRequest(Guid ReceiptLineId, decimal Quantity);

public sealed record CreateSupplierReturnRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly ReturnDate,
    string Reason,
    IReadOnlyList<SupplierReturnLineRequest> Lines,
    string? Notes = null);

/// <summary>A receipt line with goods still in hand, which could go back.</summary>
public sealed record ReturnableReceiptLine(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal ReturnedQuantity,
    decimal ReturnableQuantity,
    decimal UnitCost);

// --- Self-billing ---
//
// The one certified document of this module: an invoice we issue in a supplier's name. Everything
// above records what a supplier sent us; these are the other way round, so they carry a number, an
// ATCUD, a hash and a QR code, exactly like a sales invoice.

public sealed record SelfBilledInvoiceListItem(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    string Atcud,
    DateOnly IssueDate,
    string SupplierName,
    string SupplierTaxId,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string Status,
    bool IsAccepted)
{
    public bool IsVoided => Status == "A";
}

public sealed record SelfBilledInvoiceLine(
    Guid Id,
    int LineNumber,
    Guid? ReceiptLineId,
    Guid? ReceiptId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    string? TaxExemptionCode,
    string? TaxExemptionReason);

public sealed record SelfBilledInvoiceTaxSummary(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record SelfBilledInvoice(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    Guid SeriesId,
    string DocumentType,
    string DocumentNumber,
    string Atcud,
    DateOnly IssueDate,
    DateTime SystemEntryDateUtc,
    string Status,
    DateTime? AcceptedBySupplierAtUtc,
    PurchaseOrderSupplier Supplier,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string PrintableHash,
    string HashControl,
    string QrCodePayload,
    IReadOnlyList<SelfBilledInvoiceLine> Lines,
    IReadOnlyList<SelfBilledInvoiceTaxSummary> TaxSummary)
{
    public bool IsVoided => Status == "A";

    public bool IsAccepted => AcceptedBySupplierAtUtc is not null;
}

public sealed record SelfBilledInvoiceLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxPercentage,
    string TaxCode = "NOR",
    string TaxCountryRegion = "PT",
    string UnitOfMeasure = "UN",
    Guid? ReceiptLineId = null,
    Guid? ReceiptId = null,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null);

public sealed record IssueSelfBilledInvoiceRequest(
    Guid CompanyId,
    Guid SupplierId,
    Guid SeriesId,
    DateOnly IssueDate,
    IReadOnlyList<SelfBilledInvoiceLineRequest> Lines,
    string? SupplierAgreementReference = null);

public sealed record VoidSelfBilledInvoiceRequest(string Reason);

/// <summary>A receipt line that has not been self-billed yet.</summary>
public sealed record UnbilledReceiptLine(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    Guid SupplierId,
    string SupplierName,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal BilledQuantity,
    decimal PendingQuantity,
    decimal UnitCost);

/// <summary>The supplier's document types, for the UI selects.</summary>
public static class PurchaseDocumentTypes
{
    public static readonly (string Code, string Label)[] All =
    [
        ("FT", "Fatura"),
        ("FS", "Fatura simplificada"),
        ("FR", "Fatura-recibo"),
        ("NC", "Nota de crédito"),
        ("ND", "Nota de débito")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(type => type.Code == code).Label ?? code;

    /// <summary>
    /// A credit note gives value back rather than charging it, so it counts the other way when the
    /// figures are added up. The amounts on the document itself stay positive, as they appear on
    /// the supplier's paper.
    /// </summary>
    public static bool IsCredit(string code) => code == "NC";

    /// <summary>
    /// The documents that leave us owing money. A fatura-recibo was paid when issued, and a credit
    /// note lowers the debt, so neither is ever paid.
    /// </summary>
    public static bool IsSettledByPayment(string code) => code is "FT" or "FS" or "ND";

    public static decimal Signed(string code, decimal amount) => IsCredit(code) ? -amount : amount;
}

/// <summary>What the purchase was for. The VAT return separates deductible input tax by nature.</summary>
public static class DeductionNatures
{
    public static readonly (string Code, string Label)[] All =
    [
        ("Inventory", "Existências"),
        ("FixedAssets", "Imobilizado"),
        ("OtherGoodsAndServices", "Outros bens e serviços")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(nature => nature.Code == code).Label ?? code;
}

/// <summary>How each order status reads in the interface.</summary>
public static class PurchaseOrderStatuses
{
    public static string Describe(string status) => status switch
    {
        "Draft" => "Rascunho",
        "Placed" => "Colocada",
        "PartiallyReceived" => "Parcialmente recebida",
        "Received" => "Recebida",
        "Closed" => "Fechada",
        "Cancelled" => "Anulada",
        _ => status
    };
}
