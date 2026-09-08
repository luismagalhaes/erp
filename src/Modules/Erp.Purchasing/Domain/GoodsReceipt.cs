using Erp.FiscalPT;

namespace Erp.Purchasing.Domain;

/// <summary>
/// Goods arriving from a supplier. Not a fiscal document of ours: the transport document that came
/// with the lorry is the supplier's, and its number is recorded here as a reference, not adopted.
/// </summary>
/// <remarks>
/// This is where stock comes in. Unlike the order, a receipt is **not** rewritten once it exists —
/// it already moved stock, and editing it would leave the ledger saying one thing and the receipt
/// another. A mistake is undone with <see cref="Void"/>, which reverses both.
/// </remarks>
public sealed class GoodsReceipt
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Our own reference, e.g. <c>REC2026/1</c>. Nothing fiscal about it.</summary>
    public string Number { get; private set; } = string.Empty;

    public DateOnly ReceiptDate { get; private set; }

    public Guid WarehouseId { get; private set; }

    public GoodsReceiptStatus Status { get; private set; } = GoodsReceiptStatus.Received;

    /// <summary>The number on the supplier's delivery note or invoice. Theirs, kept as it came.</summary>
    public string? SupplierDocumentNumber { get; private set; }

    public DateOnly? SupplierDocumentDate { get; private set; }

    public SupplierSnapshot Supplier { get; private set; } = null!;

    public string? Notes { get; private set; }

    /// <summary>What arrived is worth, at the cost it came in at.</summary>
    public decimal TotalCost { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? VoidedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; private set; } = [];

    public bool IsVoided => Status == GoodsReceiptStatus.Voided;

    /// <summary>Required by EF Core.</summary>
    private GoodsReceipt()
    {
    }

    public static GoodsReceipt Create(
        Guid companyId,
        Guid supplierId,
        SupplierSnapshot supplier,
        string number,
        DateOnly receiptDate,
        Guid warehouseId,
        IReadOnlyList<GoodsReceiptLine> lines,
        string? supplierDocumentNumber = null,
        DateOnly? supplierDocumentDate = null,
        string? notes = null,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("A receipt has to say which warehouse the goods went into.", nameof(warehouseId));

        if (lines.Count == 0)
            throw new ArgumentException("A receipt with no lines received nothing.", nameof(lines));

        var receipt = new GoodsReceipt
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            Supplier = supplier,
            Number = number.Trim(),
            ReceiptDate = receiptDate,
            WarehouseId = warehouseId,
            SupplierDocumentNumber = Trim(supplierDocumentNumber),
            SupplierDocumentDate = supplierDocumentDate,
            Notes = Trim(notes),
            CreatedByUserId = createdByUserId
        };

        var lineNumber = 1;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} received no quantity.", nameof(lines));

            if (line.UnitCost < 0)
                throw new ArgumentException($"Line {lineNumber} has a negative cost.", nameof(lines));

            line.ReceiptId = receipt.Id;
            line.LineNumber = lineNumber++;
            line.LineAmount = FiscalRounding.Amount(line.Quantity * line.UnitCost);

            receipt.Lines.Add(line);
        }

        receipt.TotalCost = FiscalRounding.Amount(receipt.Lines.Sum(line => line.LineAmount));

        return receipt;
    }

    /// <summary>
    /// Undoes the receipt. The caller is responsible for reversing the stock and for giving the
    /// order back what it had received — this only records that it happened.
    /// </summary>
    public void Void(string reason, string? userId, DateTime voidedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Receipt '{Number}' is already voided.");

        Status = GoodsReceiptStatus.Voided;
        VoidReason = reason.Trim();
        VoidedByUserId = userId;
        VoidedAtUtc = voidedAtUtc;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
