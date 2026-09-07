using Erp.FiscalPT;

namespace Erp.Purchasing.Domain;

/// <summary>
/// Goods going back to the supplier. The mirror of a goods receipt: stock leaves the warehouse at
/// the cost it came in at.
/// </summary>
/// <remarks>
/// Not a fiscal document either — but note that the **transport** document for goods that actually
/// travel back <b>is</b> ours, and that is a delivery note (<c>GD</c>) issued by <c>Erp.Sales</c>.
/// This records the movement and the reason; it does not replace the transport document.
/// <para>
/// Like a receipt, and unlike an order, it is not edited once it exists: it already moved stock.
/// </para>
/// </remarks>
public sealed class SupplierReturn
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Our own reference, e.g. <c>DEV2026/1</c>. Nothing fiscal about it.</summary>
    public string Number { get; private set; } = string.Empty;

    public DateOnly ReturnDate { get; private set; }

    public Guid WarehouseId { get; private set; }

    public SupplierReturnStatus Status { get; private set; } = SupplierReturnStatus.Returned;

    /// <summary>Why the goods went back. Worth recording — it is what the credit note will cite.</summary>
    public string Reason { get; private set; } = string.Empty;

    public SupplierSnapshot Supplier { get; private set; } = null!;

    public string? Notes { get; private set; }

    public decimal TotalCost { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? VoidedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<SupplierReturnLine> Lines { get; private set; } = [];

    public bool IsVoided => Status == SupplierReturnStatus.Voided;

    /// <summary>Required by EF Core.</summary>
    private SupplierReturn()
    {
    }

    public static SupplierReturn Create(
        Guid companyId,
        Guid supplierId,
        SupplierSnapshot supplier,
        string number,
        DateOnly returnDate,
        Guid warehouseId,
        string reason,
        IReadOnlyList<SupplierReturnLine> lines,
        string? notes = null,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("A return has to say which warehouse the goods leave.", nameof(warehouseId));

        if (lines.Count == 0)
            throw new ArgumentException("A return with no lines sends nothing back.", nameof(lines));

        var supplierReturn = new SupplierReturn
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            Supplier = supplier,
            Number = number.Trim(),
            ReturnDate = returnDate,
            WarehouseId = warehouseId,
            Reason = reason.Trim(),
            Notes = Trim(notes),
            CreatedByUserId = createdByUserId
        };

        var lineNumber = 1;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} sends nothing back.", nameof(lines));

            line.ReturnId = supplierReturn.Id;
            line.LineNumber = lineNumber++;
            line.LineAmount = FiscalRounding.Amount(line.Quantity * line.UnitCost);

            supplierReturn.Lines.Add(line);
        }

        supplierReturn.TotalCost = FiscalRounding.Amount(supplierReturn.Lines.Sum(line => line.LineAmount));

        return supplierReturn;
    }

    /// <summary>
    /// Undoes the return. The caller puts the stock back; this only records that it happened.
    /// </summary>
    public void Void(string reason, string? userId, DateTime voidedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Return '{Number}' is already voided.");

        Status = SupplierReturnStatus.Voided;
        VoidReason = reason.Trim();
        VoidedByUserId = userId;
        VoidedAtUtc = voidedAtUtc;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
