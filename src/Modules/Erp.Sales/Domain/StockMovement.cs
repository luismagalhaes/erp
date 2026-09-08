using Erp.SeriesRegistry.Domain;
using Erp.FiscalPT.Documents;

namespace Erp.Sales.Domain;

/// <summary>
/// Goods movement document (guia de remessa, transporte, consignação, devolução ou ativos
/// próprios), exported under SAF-T MovementOfGoods. It is numbered, signed and made immutable
/// exactly like an invoice: the same series counter, the same hash chain, and no updates or
/// deletes once issued.
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    public Guid SeriesId { get; private set; }

    /// <summary>SAF-T MovementType: GR, GT, GA, GC or GD.</summary>
    public string MovementType { get; private set; } = string.Empty;

    public int SequenceNumber { get; private set; }

    /// <summary>SAF-T DocumentNumber, e.g. "GT A2026/2".</summary>
    public string DocumentNumber { get; private set; } = string.Empty;

    public string Atcud { get; private set; } = string.Empty;

    public DateOnly MovementDate { get; private set; }

    /// <summary>Moment the document was recorded. Signed, and never changed afterwards.</summary>
    public DateTime SystemEntryDateUtc { get; private set; }

    /// <summary>
    /// Status at issuing time. It is never updated: the current status is
    /// <see cref="EffectiveStatus"/>, derived from the append-only status changes.
    /// </summary>
    public string Status { get; private set; } = DocumentStatuses.Normal;

    public string SourceBilling { get; private set; } = SourceBillingTypes.Produced;

    // --- Counterparty snapshot ---

    public string PartyTaxId { get; private set; } = string.Empty;

    public string PartyName { get; private set; } = string.Empty;

    /// <summary>True when the counterparty is a supplier, as in a return to the supplier.</summary>
    public bool PartyIsSupplier { get; private set; }

    // --- Transport ---

    /// <summary>Where the goods are loaded. Mandatory under the goods in circulation regime.</summary>
    public MovementLocation ShipFrom { get; private set; } = null!;

    /// <summary>Where the goods are delivered.</summary>
    public MovementLocation ShipTo { get; private set; } = null!;

    /// <summary>
    /// When the transport begins. The document has to exist, and be communicated, before this
    /// moment.
    /// </summary>
    public DateTime MovementStartAtUtc { get; private set; }

    public DateTime? MovementEndAtUtc { get; private set; }

    /// <summary>Vehicle plate. Required by the transport regime, not part of the SAF-T schema.</summary>
    public string? VehiclePlate { get; private set; }

    public string? Comments { get; private set; }

    // --- Totals ---

    public decimal TotalQuantity { get; private set; }

    public decimal NetTotal { get; private set; }

    public decimal TaxPayable { get; private set; }

    /// <summary>Total with taxes. This is the value that goes into the signed string.</summary>
    public decimal GrossTotal { get; private set; }

    // --- Signature ---

    public string Hash { get; private set; } = string.Empty;

    public string PreviousHash { get; private set; } = string.Empty;

    public string HashControl { get; private set; } = string.Empty;

    public string QrCodePayload { get; private set; } = string.Empty;

    /// <summary>
    /// Code returned by the tax authority when the document is communicated before transport.
    /// Empty until that communication happens.
    /// </summary>
    public string? AtDocCodeId { get; private set; }

    public DateTime? CommunicatedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public Series Series { get; set; } = null!;

    public ICollection<StockMovementLine> Lines { get; private set; } = [];

    public ICollection<MovementStatusChange> StatusChanges { get; private set; } = [];

    /// <summary>Current status, taking the latest recorded status change into account.</summary>
    public string EffectiveStatus =>
        StatusChanges.Count == 0
            ? Status
            : StatusChanges.OrderByDescending(x => x.OccurredAtUtc).First().NewStatus;

    public bool IsVoided =>
        string.Equals(EffectiveStatus, DocumentStatuses.Voided, StringComparison.Ordinal);

    /// <summary>Required by EF Core.</summary>
    private StockMovement()
    {
    }

    /// <summary>
    /// Builds an issued movement. The signature fields are supplied by the issuing service,
    /// which is the only place allowed to compute them.
    /// </summary>
    public static StockMovement Issue(
        Guid companyId,
        Series series,
        int sequenceNumber,
        string documentNumber,
        string atcud,
        DateOnly movementDate,
        DateTime systemEntryDateUtc,
        MovementParty party,
        MovementLocation shipFrom,
        MovementLocation shipTo,
        DateTime movementStartAtUtc,
        DateTime? movementEndAtUtc,
        string? vehiclePlate,
        string? comments,
        IReadOnlyList<StockMovementLine> lines,
        decimal netTotal,
        decimal taxPayable,
        decimal grossTotal,
        string hash,
        string previousHash,
        string hashControl,
        string? createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(shipFrom);
        ArgumentNullException.ThrowIfNull(shipTo);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var movement = new StockMovement
        {
            CompanyId = companyId,
            SeriesId = series.Id,
            MovementType = series.DocumentType,
            SequenceNumber = sequenceNumber,
            DocumentNumber = documentNumber,
            Atcud = atcud,
            MovementDate = movementDate,
            SystemEntryDateUtc = systemEntryDateUtc,
            PartyTaxId = party.TaxId,
            PartyName = party.Name,
            PartyIsSupplier = party.IsSupplier,
            ShipFrom = shipFrom,
            ShipTo = shipTo,
            MovementStartAtUtc = movementStartAtUtc,
            MovementEndAtUtc = movementEndAtUtc,
            VehiclePlate = vehiclePlate,
            Comments = comments,
            TotalQuantity = lines.Sum(line => line.Quantity),
            NetTotal = netTotal,
            TaxPayable = taxPayable,
            GrossTotal = grossTotal,
            Hash = hash,
            PreviousHash = previousHash,
            HashControl = hashControl,
            CreatedByUserId = createdByUserId
        };

        foreach (var line in lines)
        {
            line.MovementId = movement.Id;
            movement.Lines.Add(line);
        }

        return movement;
    }

    /// <summary>Stores the QR code message. Set once, right after the signature is computed.</summary>
    public void AttachQrCodePayload(string payload)
    {
        if (!string.IsNullOrEmpty(QrCodePayload))
            throw new InvalidOperationException("The QR code payload of an issued document cannot be replaced.");

        QrCodePayload = payload;
    }

    /// <summary>
    /// Records the code the tax authority returned when the document was communicated. Without
    /// it the goods cannot legally start moving.
    /// </summary>
    public void Communicate(string atDocCodeId, DateTime communicatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atDocCodeId);

        if (!string.IsNullOrWhiteSpace(AtDocCodeId))
            throw new InvalidOperationException($"Document '{DocumentNumber}' was already communicated.");

        AtDocCodeId = atDocCodeId;
        CommunicatedAtUtc = communicatedAtUtc;
    }

    /// <summary>
    /// Voids the document. The header row is not touched: the new status is written as an
    /// append-only status change, which is what allows UPDATE to be denied on the table.
    /// </summary>
    public MovementStatusChange Void(string reason, string? userId, DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Document '{DocumentNumber}' is already voided.");

        var change = new MovementStatusChange
        {
            MovementId = Id,
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
