using Erp.FiscalPT;

namespace Erp.Purchasing.Domain;

/// <summary>
/// A supplier's invoice, as we recorded it. **This is not a document of ours**: the supplier
/// numbered it, signed it and communicated it. We are writing it into our books.
/// </summary>
/// <remarks>
/// Everything in <c>Erp.Sales</c> is append-only because we issued it. Here we issued nothing, so a
/// mistake in the bookkeeping is corrected by correcting it — there is no rectifying document to
/// raise, because any rectification would come from the supplier. What is not free to change is a
/// document that already moved stock: see <see cref="EnsureCanChange"/>.
/// </remarks>
public sealed class PurchaseInvoice
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SupplierId { get; private set; }

    /// <summary>Their document type: FT, FS, FR, NC or ND.</summary>
    public string DocumentType { get; private set; } = PurchaseDocumentTypes.Invoice;

    /// <summary>
    /// The number on their document, exactly as it came. Never renumbered — and not unique on its
    /// own, because two suppliers both issue their <c>FT 2026/1</c>.
    /// </summary>
    public string SupplierDocumentNumber { get; private set; } = string.Empty;

    public DateOnly SupplierDocumentDate { get; private set; }

    /// <summary>Their ATCUD, when the document carries one. Ours to check against, never to issue.</summary>
    public string? SupplierAtcud { get; private set; }

    /// <summary>When it reached us. Not the same as the date on the document.</summary>
    public DateOnly ReceivedDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>
    /// True when the tax is ours to assess: intra-Community acquisitions and article 2.º n.º 1 j).
    /// The supplier's document then carries no VAT, and the tax here is what we self-assess.
    /// </summary>
    public bool ReverseCharge { get; private set; }

    public PurchaseInvoiceStatus Status { get; private set; } = PurchaseInvoiceStatus.Recorded;

    public SupplierSnapshot Supplier { get; private set; } = null!;

    /// <summary>Where goods this invoice brings in are stored. Null when it moves no stock.</summary>
    public Guid? WarehouseId { get; private set; }

    public string? Notes { get; private set; }

    public decimal NetTotal { get; private set; }

    public decimal TaxTotal { get; private set; }

    public decimal GrossTotal { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<PurchaseInvoiceLine> Lines { get; private set; } = [];

    public ICollection<PurchaseInvoiceTaxSummary> TaxSummary { get; private set; } = [];

    public bool IsVoided => Status == PurchaseInvoiceStatus.Voided;

    /// <summary>True for a credit note, which gives value back instead of charging it.</summary>
    public bool IsCredit => PurchaseDocumentTypes.IsCredit(DocumentType);

    /// <summary>
    /// True when this document brought goods into stock itself, without a receipt before it.
    /// Never true of a credit note: goods going back leave on a return, not on the credit note,
    /// and crediting the value moves nothing.
    /// </summary>
    public bool MovedStock => !IsCredit && Lines.Any(line => line.MovesStock);

    /// <summary>Required by EF Core.</summary>
    private PurchaseInvoice()
    {
    }

    public static PurchaseInvoice Create(
        Guid companyId,
        Guid supplierId,
        SupplierSnapshot supplier,
        string documentType,
        string supplierDocumentNumber,
        DateOnly supplierDocumentDate,
        DateOnly receivedDate,
        IReadOnlyList<PurchaseInvoiceLine> lines,
        Guid? warehouseId = null,
        string? supplierAtcud = null,
        DateOnly? dueDate = null,
        bool reverseCharge = false,
        string? notes = null,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierDocumentNumber);

        if (!PurchaseDocumentTypes.IsSupported(documentType))
            throw new ArgumentException($"Unknown supplier document type '{documentType}'.", nameof(documentType));

        if (supplierDocumentDate > receivedDate)
            throw new ArgumentException("The document reached us before it was issued.", nameof(receivedDate));

        var invoice = new PurchaseInvoice
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            Supplier = supplier,
            DocumentType = documentType,
            SupplierDocumentNumber = supplierDocumentNumber.Trim(),
            SupplierDocumentDate = supplierDocumentDate,
            SupplierAtcud = Trim(supplierAtcud),
            ReceivedDate = receivedDate,
            DueDate = dueDate,
            ReverseCharge = reverseCharge,
            Notes = Trim(notes),
            CreatedByUserId = createdByUserId
        };

        invoice.ReplaceLines(lines, warehouseId);

        return invoice;
    }

    /// <summary>Rewrites the lines and recomputes the totals and the tax breakdown.</summary>
    public void ReplaceLines(IReadOnlyList<PurchaseInvoiceLine> lines, Guid? warehouseId)
    {
        ArgumentNullException.ThrowIfNull(lines);
        EnsureCanChange();

        if (lines.Count == 0)
            throw new ArgumentException("An invoice with no lines records nothing.", nameof(lines));

        Lines.Clear();

        var lineNumber = 1;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} invoices no quantity.", nameof(lines));

            line.InvoiceId = Id;
            line.LineNumber = lineNumber++;
            line.LineAmount = FiscalRounding.Amount(line.Quantity * line.UnitPrice);
            line.TaxAmount = FiscalRounding.Amount(line.LineAmount * line.TaxPercentage / 100m);

            Lines.Add(line);
        }

        // Goods that no receipt brought in have to land somewhere.
        if (MovedStock && (warehouseId is null || warehouseId == Guid.Empty))
        {
            throw new ArgumentException(
                "The invoice brings goods that no receipt brought in, so it needs a warehouse.",
                nameof(warehouseId));
        }

        WarehouseId = MovedStock ? warehouseId : null;

        RecalculateTotals();
        Touch();
    }

    public void UpdateHeader(
        DateOnly supplierDocumentDate,
        DateOnly receivedDate,
        DateOnly? dueDate,
        string? supplierAtcud,
        bool reverseCharge,
        string? notes)
    {
        EnsureCanChange();

        if (supplierDocumentDate > receivedDate)
            throw new ArgumentException("The document reached us before it was issued.", nameof(receivedDate));

        SupplierDocumentDate = supplierDocumentDate;
        ReceivedDate = receivedDate;
        DueDate = dueDate;
        SupplierAtcud = Trim(supplierAtcud);
        ReverseCharge = reverseCharge;
        Notes = Trim(notes);
        Touch();
    }

    /// <summary>
    /// Strikes the record out. The caller reverses any stock it brought in; this only records that
    /// it happened, and the row stays so the gap in the supplier's numbering is explainable.
    /// </summary>
    public void Void(string reason, string? userId, DateTime voidedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Invoice '{SupplierDocumentNumber}' is already voided.");

        Status = PurchaseInvoiceStatus.Voided;
        VoidReason = reason.Trim();
        VoidedByUserId = userId;
        VoidedAtUtc = voidedAtUtc;
    }

    /// <summary>
    /// Groups the lines by rate. Stored rather than computed on read, because this is what the VAT
    /// return is filled from and it must not drift when the tax table changes.
    /// </summary>
    private void RecalculateTotals()
    {
        NetTotal = FiscalRounding.Amount(Lines.Sum(line => line.LineAmount));
        TaxTotal = FiscalRounding.Amount(Lines.Sum(line => line.TaxAmount));
        GrossTotal = FiscalRounding.Amount(NetTotal + TaxTotal);

        TaxSummary.Clear();

        var groups = Lines
            .GroupBy(line => (line.TaxCountryRegion, line.TaxCode, line.TaxPercentage))
            .OrderBy(group => group.Key.TaxCode, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            TaxSummary.Add(new PurchaseInvoiceTaxSummary
            {
                InvoiceId = Id,
                TaxCountryRegion = group.Key.TaxCountryRegion,
                TaxCode = group.Key.TaxCode,
                TaxPercentage = group.Key.TaxPercentage,
                TaxableBase = FiscalRounding.Amount(group.Sum(line => line.LineAmount)),
                TaxAmount = FiscalRounding.Amount(group.Sum(line => line.TaxAmount))
            });
        }
    }

    /// <summary>
    /// Bookkeeping is free to be corrected — but not once this document brought goods into stock.
    /// Rewriting it then would leave the ledger saying one thing and the invoice another.
    /// </summary>
    private void EnsureCanChange()
    {
        if (IsVoided)
            throw new InvalidOperationException($"Invoice '{SupplierDocumentNumber}' is voided and cannot be changed.");

        if (MovedStock)
        {
            throw new InvalidOperationException(
                $"Invoice '{SupplierDocumentNumber}' brought goods into stock and cannot be changed. Void it instead.");
        }
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
