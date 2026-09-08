using Erp.FiscalPT.Documents;

namespace Erp.SeriesRegistry.Domain;

/// <summary>
/// Document series for a company and document type. The sequence counter lives here and is only
/// ever advanced under a database lock, so numbering has no gaps and no repeats.
/// </summary>
/// <remarks>
/// The rules it obeys — when it may issue, how the number advances, how the status moves — are not
/// written here. They live in <see cref="SeriesState"/>, in <c>Erp.FiscalPT</c>, beside the ATCUD
/// that is built from them. This is the row: what makes the rule safe under concurrency is the lock
/// on it, and a lock needs something to hold.
/// </remarks>
public sealed class Series
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Company from Erp.Core. No physical foreign key — it belongs to another module.</summary>
    public Guid CompanyId { get; set; }

    public string? EstablishmentCode { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string SeriesCode { get; set; } = string.Empty;

    /// <summary>
    /// True when this series numbers invoices we issue <b>on behalf of a supplier</b>, under article
    /// 36.º n.º 11 of the CIVA. The document type is still "FT" — it is a fatura — so nothing else
    /// tells the two apart, and they must be told apart: they belong to different SAF-T files.
    /// </summary>
    public bool SelfBilling { get; set; }

    public int InitialSequence { get; set; } = 1;

    /// <summary>Last sequence number issued. Advanced only through <see cref="TakeNextSequence"/>.</summary>
    public int CurrentSequence { get; private set; }

    /// <summary>Validation code returned by the tax authority when the series is communicated.</summary>
    public string? ValidationCode { get; private set; }

    public DateTime? CommunicatedAtUtc { get; private set; }

    public SeriesStatus Status { get; private set; } = SeriesStatus.Created;

    /// <summary>
    /// What documents of this series do to stock. Defaults from the document type when the series
    /// is created, and can be changed: the same type can be used differently by different
    /// businesses.
    /// </summary>
    public StockEffect StockEffect { get; set; } = StockEffect.None;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? FinalizedAtUtc { get; private set; }

    public string? CreatedByUserId { get; set; }

    public byte[]? RowVersion { get; set; }

    /// <summary>The rule this row is a snapshot of.</summary>
    private SeriesState State => new(Status, ValidationCode, InitialSequence, CurrentSequence);

    public bool CanIssue => State.CanIssue;

    /// <summary>Records the validation code returned by the tax authority.</summary>
    public void Communicate(string validationCode, DateTime communicatedAtUtc)
    {
        Apply(State.Communicate(validationCode));
        CommunicatedAtUtc = communicatedAtUtc;
    }

    /// <summary>Advances and returns the next sequence number. Call inside the issuing transaction.</summary>
    public int TakeNextSequence()
    {
        try
        {
            var (state, sequence) = State.TakeNextSequence();
            Apply(state);

            return sequence;
        }
        catch (InvalidOperationException ex)
        {
            // The rule has no idea which series it is; the row does, and the caller needs to know.
            throw new InvalidOperationException($"Series '{SeriesCode}': {ex.Message}", ex);
        }
    }

    public void Finalize(DateTime finalizedAtUtc)
    {
        if (Status == SeriesStatus.Finalized)
            return;

        Apply(State.Finalize());
        FinalizedAtUtc = finalizedAtUtc;
    }

    private void Apply(SeriesState state)
    {
        Status = state.Status;
        ValidationCode = state.ValidationCode;
        CurrentSequence = state.CurrentSequence;
    }
}
