using Erp.FiscalPT.Documents;
using Erp.SeriesRegistry.Domain;

namespace Erp.Purchasing.Domain;

/// <summary>
/// An invoice <b>we</b> issued in the supplier's name, under article 36.º n.º 11 of the CIVA. The
/// only certified document this module produces.
/// </summary>
/// <remarks>
/// Everything else in <c>Erp.Purchasing</c> is bookkeeping and therefore editable. This is the
/// opposite: we issued it, so it obeys the same rules as a sales invoice — numbered from a
/// communicated series, signed into the chain, carrying an ATCUD and a QR code, never updated, and
/// voided by appending a status change.
/// <para>
/// It titles a <b>sale of the supplier's</b>. That is why it goes into its own SAF-T file, of type
/// <c>"S"</c>, one per supplier and with the supplier's tax id in the header — not into our billing
/// file, where none of these belong.
/// </para>
/// </remarks>
public sealed class SelfBilledInvoice
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    /// <summary>The supplier from Erp.Core. No physical foreign key — it belongs to another module.</summary>
    public Guid SupplierId { get; private set; }

    public Guid SeriesId { get; private set; }

    /// <summary>"FT" or "NC". It is a fatura like any other; what it is not is a sale of ours.</summary>
    public string DocumentType { get; private set; } = SalesDocumentTypes.Invoice;

    public int SequenceNumber { get; private set; }

    /// <summary>SAF-T InvoiceNo, e.g. "FT AF2026/2".</summary>
    public string DocumentNumber { get; private set; } = string.Empty;

    public string Atcud { get; private set; } = string.Empty;

    public DateOnly IssueDate { get; private set; }

    /// <summary>Moment the document was recorded. Signed, and never changed afterwards.</summary>
    public DateTime SystemEntryDateUtc { get; private set; }

    /// <summary>Status at issuing time; the current one is <see cref="EffectiveStatus"/>.</summary>
    public string Status { get; private set; } = DocumentStatuses.Normal;

    public string EffectiveStatus =>
        StatusChanges.Count == 0
            ? Status
            : StatusChanges.OrderByDescending(x => x.OccurredAtUtc).First().NewStatus;

    public bool IsVoided =>
        string.Equals(EffectiveStatus, DocumentStatuses.Voided, StringComparison.Ordinal);

    /// <summary>
    /// When the supplier accepted this document. Article 36.º n.º 11 requires acceptance of <b>each
    /// document</b>, not merely a standing agreement, so until this is recorded the document exists
    /// but the regime is not satisfied.
    /// </summary>
    public DateTime? AcceptedBySupplierAtUtc { get; private set; }

    public bool IsAccepted => AcceptedBySupplierAtUtc is not null;

    /// <summary>
    /// The prior agreement with the supplier that the same article requires, recorded so that what
    /// authorises the document is on the document and not only in a drawer.
    /// </summary>
    public string? SupplierAgreementReference { get; private set; }

    /// <summary>The supplier as it stood when we issued. Copied, never referenced.</summary>
    public SupplierSnapshot Supplier { get; private set; } = null!;

    public decimal NetTotal { get; private set; }

    public decimal TaxPayable { get; private set; }

    /// <summary>Total with taxes. This is the value that goes into the signed string.</summary>
    public decimal GrossTotal { get; private set; }

    /// <summary>Base64 RSA signature of this document.</summary>
    public string Hash { get; private set; } = string.Empty;

    /// <summary>Signature of the previous document in the same series; empty for the first one.</summary>
    public string PreviousHash { get; private set; } = string.Empty;

    public string HashControl { get; private set; } = string.Empty;

    public string QrCodePayload { get; private set; } = string.Empty;

    public string? CreatedByUserId { get; private set; }

    public Series Series { get; set; } = null!;

    public ICollection<SelfBilledInvoiceLine> Lines { get; private set; } = [];

    public ICollection<SelfBilledInvoiceTaxSummary> TaxSummaries { get; private set; } = [];

    public ICollection<SelfBilledInvoiceStatusChange> StatusChanges { get; private set; } = [];

    /// <summary>Required by EF Core.</summary>
    private SelfBilledInvoice()
    {
    }

    /// <summary>
    /// Builds an issued document. The signature fields are supplied by the issuing service, which is
    /// the only place allowed to compute them.
    /// </summary>
    public static SelfBilledInvoice Issue(
        Guid companyId,
        Guid supplierId,
        Series series,
        int sequenceNumber,
        string documentNumber,
        string atcud,
        DateOnly issueDate,
        DateTime systemEntryDateUtc,
        SupplierSnapshot supplier,
        IReadOnlyList<SelfBilledInvoiceLine> lines,
        IReadOnlyList<SelfBilledInvoiceTaxSummary> taxSummaries,
        decimal netTotal,
        decimal taxPayable,
        decimal grossTotal,
        string hash,
        string previousHash,
        string hashControl,
        string? createdByUserId,
        string? supplierAgreementReference = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        // The supplier is the taxable entity of the file this document ends up in. Without their
        // tax id there is no file to put it in, and no way to tell whose sale it was.
        if (string.IsNullOrWhiteSpace(supplier.TaxId))
            throw new ArgumentException("A self-billed invoice requires the supplier's tax id.", nameof(supplier));

        var document = new SelfBilledInvoice
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            SeriesId = series.Id,
            DocumentType = series.DocumentType,
            SequenceNumber = sequenceNumber,
            DocumentNumber = documentNumber,
            Atcud = atcud,
            IssueDate = issueDate,
            SystemEntryDateUtc = systemEntryDateUtc,
            Status = DocumentStatuses.Normal,
            Supplier = supplier,
            NetTotal = netTotal,
            TaxPayable = taxPayable,
            GrossTotal = grossTotal,
            Hash = hash,
            PreviousHash = previousHash,
            HashControl = hashControl,
            CreatedByUserId = createdByUserId,
            SupplierAgreementReference = supplierAgreementReference
        };

        foreach (var line in lines)
        {
            line.InvoiceId = document.Id;
            document.Lines.Add(line);
        }

        foreach (var summary in taxSummaries)
        {
            summary.InvoiceId = document.Id;
            document.TaxSummaries.Add(summary);
        }

        return document;
    }

    /// <summary>Stores the QR code message. Set once, right after the signature is computed.</summary>
    public void AttachQrCodePayload(string payload)
    {
        if (!string.IsNullOrEmpty(QrCodePayload))
            throw new InvalidOperationException("The QR code payload of an issued document cannot be replaced.");

        QrCodePayload = payload;
    }

    /// <summary>
    /// Records the supplier's acceptance. Recorded once: a second acceptance would mean the first
    /// one was not what the regime asked for.
    /// </summary>
    public void Accept(DateTime acceptedAtUtc)
    {
        if (IsVoided)
            throw new InvalidOperationException($"Document '{DocumentNumber}' is voided and cannot be accepted.");

        if (IsAccepted)
            throw new InvalidOperationException($"Document '{DocumentNumber}' was already accepted by the supplier.");

        AcceptedBySupplierAtUtc = acceptedAtUtc;
    }

    /// <summary>
    /// Voids the document. The header row is not touched: the new status is appended, which is what
    /// allows UPDATE to be denied on the document table.
    /// </summary>
    public SelfBilledInvoiceStatusChange Void(string reason, string? userId, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Document '{DocumentNumber}' is already voided.");

        var change = new SelfBilledInvoiceStatusChange
        {
            InvoiceId = Id,
            PreviousStatus = EffectiveStatus,
            NewStatus = DocumentStatuses.Voided,
            Reason = reason,
            UserId = userId,
            OccurredAtUtc = occurredAtUtc
        };

        StatusChanges.Add(change);

        return change;
    }
}
