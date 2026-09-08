namespace Erp.SeriesRegistry.Domain;

/// <summary>
/// The counter behind an internal document number — <c>REC2026/7</c>, <c>ENC2026/7</c>,
/// <c>DEV2026/7</c>. One row per company, prefix and year.
/// </summary>
/// <remarks>
/// These numbers are ours and carry no fiscal meaning: the tax authority knows nothing about them,
/// and they take no signature and no ATCUD. What they do have to be is <b>sequential</b> — a
/// warehouse worker reading <c>REC2026/7</c> expects a seventh receipt, and a number that failed and
/// was never used leaves a hole nobody can explain.
/// <para>
/// Deriving the next one from <c>MAX</c> of what exists cannot give that: two requests read the same
/// maximum and propose the same number, and the unique index refuses one of them. A row that can be
/// locked is the difference, which is why this exists at all rather than being counted in place.
/// </para>
/// <para>
/// It lives beside <see cref="Series"/> and not inside it. A fiscal series is communicated to the
/// tax authority and may only issue once it has a validation code; passing an internal number
/// through that would mean weakening <c>CanIssue</c>, which is a fiscal guard, to accommodate a
/// document that is not fiscal.
/// </para>
/// </remarks>
public sealed class DocumentCounter
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    /// <summary>"REC", "ENC", "DEV" — what the number is for.</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>Counters restart each year, which is what puts the year in the number.</summary>
    public int Year { get; private set; }

    public int LastNumber { get; private set; }

    public byte[]? RowVersion { get; set; }

    /// <summary>Required by EF Core.</summary>
    private DocumentCounter()
    {
    }

    public static DocumentCounter Start(Guid companyId, string prefix, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return new DocumentCounter
        {
            CompanyId = companyId,
            Prefix = prefix.Trim().ToUpperInvariant(),
            Year = year
        };
    }

    /// <summary>
    /// Advances and returns the next number. Call inside the transaction that writes the document,
    /// with the row already locked: the count is only safe for as long as the lock is held.
    /// </summary>
    public int TakeNext() => ++LastNumber;

    /// <summary>The number as it is written on the document and read back by people.</summary>
    public static string Format(string prefix, int year, int number) => $"{prefix}{year}/{number}";
}
