using Erp.FiscalPT;

namespace Erp.Purchasing.Domain;

/// <summary>
/// An order placed with a supplier. Not a fiscal document: nobody signs it, it carries no ATCUD and
/// it is numbered by us for our own use. It commits us to buy and tells us what is still owed.
/// </summary>
/// <remarks>
/// Deliberately mutable, unlike everything in <c>Erp.Sales</c>. Nothing here was issued to a tax
/// authority, so a mistake is corrected by correcting it. What is not free to change is a line that
/// has already received goods — see <see cref="EnsureCanChangeLines"/>.
/// </remarks>
public sealed class PurchaseOrder
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    /// <summary>Supplier from Erp.Core. No physical foreign key — it lives in another module.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Our own reference, e.g. <c>ENC2026/1</c>. Sequential per company, not per AT series.</summary>
    public string Number { get; private set; } = string.Empty;

    public DateOnly OrderDate { get; private set; }

    /// <summary>When the goods are expected. Null when the supplier has not committed to a date.</summary>
    public DateOnly? ExpectedDate { get; private set; }

    /// <summary>Where the goods are to be received. Decided at ordering so the receipt has a default.</summary>
    public Guid WarehouseId { get; private set; }

    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;

    public SupplierSnapshot Supplier { get; private set; } = null!;

    public string? Notes { get; private set; }

    public decimal NetTotal { get; private set; }

    public decimal TaxTotal { get; private set; }

    public decimal GrossTotal { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? ClosedReason { get; private set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; private set; } = [];

    /// <summary>True while the order is still expecting goods.</summary>
    public bool IsOpen => Status is PurchaseOrderStatus.Placed or PurchaseOrderStatus.PartiallyReceived;

    public bool HasReceipts => Lines.Any(line => line.ReceivedQuantity > 0);

    /// <summary>Required by EF Core.</summary>
    private PurchaseOrder()
    {
    }

    public static PurchaseOrder Create(
        Guid companyId,
        Guid supplierId,
        SupplierSnapshot supplier,
        string number,
        DateOnly orderDate,
        Guid warehouseId,
        IReadOnlyList<PurchaseOrderLine> lines,
        DateOnly? expectedDate = null,
        string? notes = null,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("An order has to say where the goods are to be received.", nameof(warehouseId));

        var order = new PurchaseOrder
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            Supplier = supplier,
            Number = number.Trim(),
            OrderDate = orderDate,
            ExpectedDate = expectedDate,
            WarehouseId = warehouseId,
            Notes = Trim(notes),
            CreatedByUserId = createdByUserId
        };

        order.ReplaceLines(lines);

        return order;
    }

    /// <summary>Replaces every line and recomputes the totals.</summary>
    public void ReplaceLines(IReadOnlyList<PurchaseOrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureCanChangeLines();

        if (lines.Count == 0)
            throw new ArgumentException("An order with no lines orders nothing.", nameof(lines));

        Lines.Clear();

        var lineNumber = 1;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} orders no quantity.", nameof(lines));

            if (line.UnitPrice < 0)
                throw new ArgumentException($"Line {lineNumber} has a negative price.", nameof(lines));

            line.OrderId = Id;
            line.LineNumber = lineNumber++;
            line.LineAmount = FiscalRounding.Amount(line.Quantity * line.UnitPrice);
            line.TaxAmount = FiscalRounding.Amount(line.LineAmount * line.TaxPercentage / 100m);

            Lines.Add(line);
        }

        RecalculateTotals();
        Touch();
    }

    public void UpdateHeader(DateOnly orderDate, DateOnly? expectedDate, Guid warehouseId, string? notes)
    {
        EnsureNotFinished();

        if (warehouseId == Guid.Empty)
            throw new ArgumentException("An order has to say where the goods are to be received.", nameof(warehouseId));

        OrderDate = orderDate;
        ExpectedDate = expectedDate;
        WarehouseId = warehouseId;
        Notes = Trim(notes);
        Touch();
    }

    /// <summary>Sends the order to the supplier. From here it counts as owed to us.</summary>
    public void Place()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Order '{Number}' has already been placed.");

        Status = PurchaseOrderStatus.Placed;
        Touch();
    }

    /// <summary>
    /// Records that goods arrived against a line, and moves the order's status with it. Receiving
    /// more than was ordered is allowed — suppliers do it — and is the caller's business to accept
    /// or refuse before getting here.
    /// </summary>
    public void RegisterReceipt(Guid lineId, decimal quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (Status is PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Order '{Number}' has not been placed yet.");

        if (Status is PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Order '{Number}' was cancelled.");

        var line = Lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new ArgumentException($"Line '{lineId}' does not belong to this order.", nameof(lineId));

        line.ReceivedQuantity += quantity;

        Status = ReceiptStatus();
        Touch();
    }

    /// <summary>Undoes a receipt, when the receipt that caused it is itself undone.</summary>
    public void ReverseReceipt(Guid lineId, decimal quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var line = Lines.FirstOrDefault(x => x.Id == lineId)
            ?? throw new ArgumentException($"Line '{lineId}' does not belong to this order.", nameof(lineId));

        line.ReceivedQuantity = Math.Max(0m, line.ReceivedQuantity - quantity);

        Status = ReceiptStatus();
        Touch();
    }

    /// <summary>
    /// What the order's status is, given what has arrived. Derived rather than tracked, so
    /// registering and reversing a receipt can never leave the two disagreeing.
    /// </summary>
    private PurchaseOrderStatus ReceiptStatus()
    {
        if (!HasReceipts)
            return PurchaseOrderStatus.Placed;

        return Lines.All(line => line.IsFullyReceived)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
    }

    /// <summary>
    /// Closes an order that will not be completed. The rest is written off rather than left owed
    /// for ever — what a supplier still owes has to mean something.
    /// </summary>
    public void Close(string reason, DateTime closedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureNotFinished();

        if (Status == PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("A draft is cancelled, not closed.");

        Status = PurchaseOrderStatus.Closed;
        ClosedReason = reason.Trim();
        ClosedAtUtc = closedAtUtc;
        Touch();
    }

    /// <summary>Calls the order off. Only possible while nothing has arrived.</summary>
    public void Cancel(string reason, DateTime cancelledAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureNotFinished();

        if (HasReceipts)
            throw new InvalidOperationException($"Order '{Number}' has goods received and can only be closed.");

        Status = PurchaseOrderStatus.Cancelled;
        ClosedReason = reason.Trim();
        ClosedAtUtc = cancelledAtUtc;
        Touch();
    }

    private void RecalculateTotals()
    {
        NetTotal = FiscalRounding.Amount(Lines.Sum(line => line.LineAmount));
        TaxTotal = FiscalRounding.Amount(Lines.Sum(line => line.TaxAmount));
        GrossTotal = FiscalRounding.Amount(NetTotal + TaxTotal);
    }

    /// <summary>
    /// Lines may be rewritten freely until goods start arriving. After that, changing them would
    /// silently rewrite what a receipt was measured against.
    /// </summary>
    private void EnsureCanChangeLines()
    {
        EnsureNotFinished();

        if (HasReceipts)
            throw new InvalidOperationException($"Order '{Number}' already has goods received and its lines cannot change.");
    }

    private void EnsureNotFinished()
    {
        if (Status is PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Order '{Number}' is finished and cannot be changed.");
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
