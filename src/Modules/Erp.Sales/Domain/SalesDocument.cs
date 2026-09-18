using Erp.SeriesRegistry.Domain;
using Erp.FiscalPT.Documents;

namespace Erp.Sales.Domain;

/// <summary>
/// A fiscally relevant document. Once issued it is never updated or deleted: the fiscal fields
/// have no public setters, corrections are made through a rectifying document, and voiding is
/// recorded as a <see cref="DocumentStatusChange"/> row.
/// </summary>
public sealed class SalesDocument
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SeriesId { get; private set; }

    public string DocumentType { get; private set; } = string.Empty;

    public int SequenceNumber { get; private set; }

    /// <summary>SAF-T InvoiceNo, e.g. "FT A2026/2".</summary>
    public string DocumentNumber { get; private set; } = string.Empty;

    public string Atcud { get; private set; } = string.Empty;

    public DateOnly DocumentDate { get; private set; }

    /// <summary>Moment the document was recorded. Signed, and never changed afterwards.</summary>
    public DateTime SystemEntryDateUtc { get; private set; }

    /// <summary>
    /// Status at issuing time. It is never updated: the current status is
    /// <see cref="EffectiveStatus"/>, derived from the append-only status changes.
    /// </summary>
    public string Status { get; private set; } = DocumentStatuses.Normal;

    /// <summary>Current status, taking the latest recorded status change into account.</summary>
    public string EffectiveStatus =>
        StatusChanges.Count == 0
            ? Status
            : StatusChanges.OrderByDescending(x => x.OccurredAtUtc).First().NewStatus;

    public bool IsVoided =>
        string.Equals(EffectiveStatus, DocumentStatuses.Voided, StringComparison.Ordinal);

    public string SourceBilling { get; private set; } = SourceBillingTypes.Produced;

    public string CustomerTaxId { get; private set; } = string.Empty;

    public string CustomerName { get; private set; } = string.Empty;

    public string? CustomerAddress { get; private set; }

    public string? CustomerPostalCode { get; private set; }

    public string? CustomerCity { get; private set; }

    public string CustomerCountry { get; private set; } = "PT";

    /// <summary>Until when the customer has to pay. Not signed, and not part of the SAF-T.</summary>
    public DateOnly? DueDate { get; private set; }

    public decimal NetTotal { get; private set; }

    public decimal TaxPayable { get; private set; }

    /// <summary>Total with taxes. This is the value that goes into the signed string.</summary>
    public decimal GrossTotal { get; private set; }

    /// <summary>Base64 RSA signature of this document.</summary>
    public string Hash { get; private set; } = string.Empty;

    /// <summary>Signature of the previous document in the same series; empty for the first one.</summary>
    public string PreviousHash { get; private set; } = string.Empty;

    /// <summary>Version of the private key used, so keys can be rotated without breaking history.</summary>
    public string HashControl { get; private set; } = string.Empty;

    public string QrCodePayload { get; private set; } = string.Empty;

    // --- Rectification, for credit and debit notes ---

    public Guid? RectifiedDocumentId { get; private set; }

    /// <summary>Number of the rectified document, copied at issuing time.</summary>
    public string? RectifiedDocumentNumber { get; private set; }

    public string? RectificationReason { get; private set; }

    /// <summary>True when this document corrects another one.</summary>
    public bool IsRectifying => RectifiedDocumentId is not null;

    public string? CreatedByUserId { get; private set; }

    public Series Series { get; set; } = null!;

    public ICollection<SalesDocumentLine> Lines { get; private set; } = [];

    public ICollection<DocumentTaxSummary> TaxSummaries { get; private set; } = [];

    public ICollection<DocumentStatusChange> StatusChanges { get; private set; } = [];

    /// <summary>How a fatura-recibo was paid. Empty for every other type.</summary>
    public ICollection<SalesDocumentPayment> Payments { get; private set; } = [];

    /// <summary>Quantity times price over all lines, before discounts.</summary>
    public decimal GrossLinesTotal => Lines.Sum(line => line.GrossAmount);

    public decimal DiscountTotal => Lines.Sum(line => line.DiscountAmount);

    /// <summary>Required by EF Core.</summary>
    private SalesDocument()
    {
    }

    /// <summary>
    /// Builds an issued document. The signature fields are supplied by the issuing service,
    /// which is the only place allowed to compute them.
    /// </summary>
    public static SalesDocument Issue(
        Guid companyId,
        Series series,
        int sequenceNumber,
        string documentNumber,
        string atcud,
        DateOnly documentDate,
        DateTime systemEntryDateUtc,
        CustomerSnapshot customer,
        IReadOnlyList<SalesDocumentLine> lines,
        IReadOnlyList<DocumentTaxSummary> taxSummaries,
        decimal netTotal,
        decimal taxPayable,
        decimal grossTotal,
        string hash,
        string previousHash,
        string hashControl,
        string? createdByUserId,
        RectifiedDocument? rectifies = null,
        DateOnly? dueDate = null,
        IReadOnlyList<SalesDocumentPayment>? payments = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var document = new SalesDocument
        {
            CompanyId = companyId,
            SeriesId = series.Id,
            DocumentType = series.DocumentType,
            SequenceNumber = sequenceNumber,
            DocumentNumber = documentNumber,
            Atcud = atcud,
            DocumentDate = documentDate,
            SystemEntryDateUtc = systemEntryDateUtc,
            Status = DocumentStatuses.Normal,
            SourceBilling = SourceBillingTypes.Produced,
            CustomerTaxId = customer.TaxId,
            CustomerName = customer.Name,
            CustomerAddress = customer.Address,
            CustomerPostalCode = customer.PostalCode,
            CustomerCity = customer.City,
            CustomerCountry = customer.Country,
            DueDate = dueDate,
            NetTotal = netTotal,
            TaxPayable = taxPayable,
            GrossTotal = grossTotal,
            Hash = hash,
            PreviousHash = previousHash,
            HashControl = hashControl,
            CreatedByUserId = createdByUserId,
            RectifiedDocumentId = rectifies?.DocumentId,
            RectifiedDocumentNumber = rectifies?.DocumentNumber,
            RectificationReason = rectifies?.Reason
        };

        foreach (var line in lines)
        {
            line.DocumentId = document.Id;
            document.Lines.Add(line);
        }

        foreach (var summary in taxSummaries)
        {
            summary.DocumentId = document.Id;
            document.TaxSummaries.Add(summary);
        }

        foreach (var payment in payments ?? [])
        {
            payment.DocumentId = document.Id;
            document.Payments.Add(payment);
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
    /// Voids the document. The header row is not touched: the new status is written as an
    /// append-only status change, which is what allows UPDATE to be denied on the document table.
    /// </summary>
    public DocumentStatusChange Void(string reason, string? userId, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Document '{DocumentNumber}' is already voided.");

        var change = new DocumentStatusChange
        {
            DocumentId = Id,
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
