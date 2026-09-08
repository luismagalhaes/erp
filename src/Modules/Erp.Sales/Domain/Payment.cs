using Erp.SeriesRegistry.Domain;
using Erp.FiscalPT.Documents;

namespace Erp.Sales.Domain;

/// <summary>
/// Receipt, exported under SAF-T Payments. A receipt is a fiscally relevant document: it is
/// numbered from a series, signed into the same hash chain and never altered once issued. Its
/// lines say which invoices it settles and by how much.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SeriesId { get; private set; }

    /// <summary>SAF-T PaymentType: RC or RG.</summary>
    public string PaymentType { get; private set; } = string.Empty;

    public int SequenceNumber { get; private set; }

    /// <summary>SAF-T PaymentRefNo, e.g. "RC A2026/2".</summary>
    public string PaymentRefNo { get; private set; } = string.Empty;

    public string Atcud { get; private set; } = string.Empty;

    public DateOnly TransactionDate { get; private set; }

    /// <summary>Moment the receipt was recorded. Signed, and never changed afterwards.</summary>
    public DateTime SystemEntryDateUtc { get; private set; }

    /// <summary>
    /// Status at issuing time. It is never updated: the current status is
    /// <see cref="EffectiveStatus"/>, derived from the append-only status changes.
    /// </summary>
    public string Status { get; private set; } = DocumentStatuses.Normal;

    /// <summary>SAF-T SourcePayment: P produced, I integrated, M manual.</summary>
    public string SourcePayment { get; private set; } = SourceBillingTypes.Produced;

    public string PartyTaxId { get; private set; } = string.Empty;

    public string PartyName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    // --- Totals ---

    public decimal NetTotal { get; private set; }

    /// <summary>
    /// Zero outside the cash VAT regime: the VAT was already accounted for on the invoice, so
    /// the receipt only moves money.
    /// </summary>
    public decimal TaxPayable { get; private set; }

    /// <summary>Total received. This is the value that goes into the signed string.</summary>
    public decimal GrossTotal { get; private set; }

    // --- Signature ---

    public string Hash { get; private set; } = string.Empty;

    public string PreviousHash { get; private set; } = string.Empty;

    public string HashControl { get; private set; } = string.Empty;

    public string QrCodePayload { get; private set; } = string.Empty;

    public string? CreatedByUserId { get; private set; }

    public Series Series { get; set; } = null!;

    public ICollection<PaymentLine> Lines { get; private set; } = [];

    public ICollection<PaymentMethodEntry> Methods { get; private set; } = [];

    public ICollection<PaymentStatusChange> StatusChanges { get; private set; } = [];

    /// <summary>Current status, taking the latest recorded status change into account.</summary>
    public string EffectiveStatus =>
        StatusChanges.Count == 0
            ? Status
            : StatusChanges.OrderByDescending(x => x.OccurredAtUtc).First().NewStatus;

    public bool IsVoided =>
        string.Equals(EffectiveStatus, DocumentStatuses.Voided, StringComparison.Ordinal);

    /// <summary>Required by EF Core.</summary>
    private Payment()
    {
    }

    /// <summary>
    /// Builds an issued receipt. The signature fields are supplied by the issuing service,
    /// which is the only place allowed to compute them.
    /// </summary>
    public static Payment Issue(
        Guid companyId,
        Series series,
        int sequenceNumber,
        string paymentRefNo,
        string atcud,
        DateOnly transactionDate,
        DateTime systemEntryDateUtc,
        PaymentParty party,
        string? description,
        IReadOnlyList<PaymentLine> lines,
        IReadOnlyList<PaymentMethodEntry> methods,
        decimal grossTotal,
        string hash,
        string previousHash,
        string hashControl,
        string? createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentRefNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var payment = new Payment
        {
            CompanyId = companyId,
            SeriesId = series.Id,
            PaymentType = series.DocumentType,
            SequenceNumber = sequenceNumber,
            PaymentRefNo = paymentRefNo,
            Atcud = atcud,
            TransactionDate = transactionDate,
            SystemEntryDateUtc = systemEntryDateUtc,
            PartyTaxId = party.TaxId,
            PartyName = party.Name,
            Description = description,
            NetTotal = grossTotal,
            TaxPayable = 0m,
            GrossTotal = grossTotal,
            Hash = hash,
            PreviousHash = previousHash,
            HashControl = hashControl,
            CreatedByUserId = createdByUserId
        };

        foreach (var line in lines)
        {
            line.PaymentId = payment.Id;
            payment.Lines.Add(line);
        }

        foreach (var method in methods)
        {
            method.PaymentId = payment.Id;
            payment.Methods.Add(method);
        }

        return payment;
    }

    /// <summary>Stores the QR code message. Set once, right after the signature is computed.</summary>
    public void AttachQrCodePayload(string payload)
    {
        if (!string.IsNullOrEmpty(QrCodePayload))
            throw new InvalidOperationException("The QR code payload of an issued document cannot be replaced.");

        QrCodePayload = payload;
    }

    /// <summary>
    /// Voids the receipt. The header row is not touched: the new status is written as an
    /// append-only status change, which is what allows UPDATE to be denied on the table. The
    /// settled invoices go back to being owed.
    /// </summary>
    public PaymentStatusChange Void(string reason, string? userId, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Receipt '{PaymentRefNo}' is already voided.");

        var change = new PaymentStatusChange
        {
            PaymentId = Id,
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
