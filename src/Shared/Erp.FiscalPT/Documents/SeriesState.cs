namespace Erp.FiscalPT.Documents;

/// <summary>
/// The rules a document series obeys: when it may issue, how its number advances, and how its
/// status moves. Pure and immutable — every operation returns the next state rather than mutating.
/// </summary>
/// <remarks>
/// This lives beside <see cref="Atcud"/> and <see cref="DocumentNumber"/> because it is the same
/// body of law: the ATCUD is built from a series' validation code and the sequence this hands out.
/// Keeping them apart is what makes that connection something you have to know rather than see.
/// <para>
/// What is <b>not</b> here is the row: a series is a database row precisely so that two documents
/// cannot take the same number, and that lock belongs to a storage layer. This is only the rule it
/// enforces once the row is held.
/// </para>
/// </remarks>
/// <param name="InitialSequence">
/// Where the series starts. Above 1 when a series continues numbering from another system.
/// </param>
/// <param name="CurrentSequence">The last number handed out; zero before the first document.</param>
public readonly record struct SeriesState(
    SeriesStatus Status,
    string? ValidationCode,
    int InitialSequence,
    int CurrentSequence)
{
    /// <summary>A series as it stands the moment it is created, before being communicated.</summary>
    public static SeriesState New(int initialSequence = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialSequence, 1);

        return new SeriesState(SeriesStatus.Created, null, initialSequence, 0);
    }

    /// <summary>
    /// Whether a document may be issued. A series with no validation code has not been accepted by
    /// the tax authority, and issuing on it would produce documents with no valid ATCUD.
    /// </summary>
    public bool CanIssue =>
        Status is SeriesStatus.Communicated or SeriesStatus.Active
        && !string.IsNullOrWhiteSpace(ValidationCode);

    public bool IsFinalized => Status == SeriesStatus.Finalized;

    /// <summary>Records the validation code the tax authority returned.</summary>
    public SeriesState Communicate(string validationCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validationCode);

        if (IsFinalized)
            throw new InvalidOperationException("A finalized series cannot be communicated again.");

        return this with { Status = SeriesStatus.Communicated, ValidationCode = validationCode };
    }

    /// <summary>
    /// Hands out the next number and returns the state that goes with it. Call inside the issuing
    /// transaction, with the series' row held: the caller's lock is what makes this safe.
    /// </summary>
    public (SeriesState State, int Sequence) TakeNextSequence()
    {
        if (!CanIssue)
            throw new InvalidOperationException("The series is not available for issuing documents.");

        // A series that starts above 1 jumps there on its first document rather than counting up
        // to it, so the first number issued is the one that was declared.
        var previous = CurrentSequence == 0 && InitialSequence > 1
            ? InitialSequence - 1
            : CurrentSequence;

        var next = previous + 1;

        return (this with { Status = SeriesStatus.Active, CurrentSequence = next }, next);
    }

    /// <summary>Closes the series. Doing it twice changes nothing.</summary>
    public SeriesState Finalize() =>
        IsFinalized ? this : this with { Status = SeriesStatus.Finalized };

    /// <summary>
    /// Cancels a series communicated by mistake. Only allowed while nothing has been issued on it
    /// yet: the tax authority requires an attestation that no document used the series, and that is
    /// only true while the status is still <see cref="SeriesStatus.Communicated"/> — once
    /// <see cref="TakeNextSequence"/> has run once, that attestation would be false.
    /// </summary>
    public SeriesState Cancel()
    {
        if (Status != SeriesStatus.Communicated)
        {
            throw new InvalidOperationException(
                "Only a communicated series with no document issued yet can be cancelled.");
        }

        return this with { Status = SeriesStatus.Cancelled };
    }
}
