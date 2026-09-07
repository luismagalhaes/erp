namespace Erp.Sales.Domain;

/// <summary>
/// Document series for a company and document type. The sequence counter lives here and is
/// only ever advanced under a database lock, so numbering has no gaps and no repeats.
/// </summary>
public sealed class Series
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Company from Erp.Core. No physical foreign key - it lives in another database.</summary>
    public Guid CompanyId { get; set; }

    public string? EstablishmentCode { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string SeriesCode { get; set; } = string.Empty;

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

    public bool CanIssue => Status is SeriesStatus.Communicated or SeriesStatus.Active
                            && !string.IsNullOrWhiteSpace(ValidationCode);

    /// <summary>Records the validation code returned by the tax authority.</summary>
    public void Communicate(string validationCode, DateTime communicatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validationCode);

        if (Status == SeriesStatus.Finalized)
            throw new InvalidOperationException("A finalized series cannot be communicated again.");

        ValidationCode = validationCode;
        CommunicatedAtUtc = communicatedAtUtc;
        Status = SeriesStatus.Communicated;
    }

    /// <summary>Advances and returns the next sequence number. Call inside the issuing transaction.</summary>
    public int TakeNextSequence()
    {
        if (!CanIssue)
            throw new InvalidOperationException($"Series '{SeriesCode}' is not available for issuing documents.");

        if (CurrentSequence == 0 && InitialSequence > 1)
            CurrentSequence = InitialSequence - 1;

        CurrentSequence++;
        Status = SeriesStatus.Active;

        return CurrentSequence;
    }

    public void Finalize(DateTime finalizedAtUtc)
    {
        if (Status == SeriesStatus.Finalized)
            return;

        Status = SeriesStatus.Finalized;
        FinalizedAtUtc = finalizedAtUtc;
    }
}
