namespace Erp.FiscalPT.Saft;

/// <summary>One invoice settled by a receipt, with the amount applied to it.</summary>
public sealed class SaftPaymentLine
{
    public int LineNumber { get; init; }

    /// <summary>Number of the settled document, e.g. "FT A2026/2".</summary>
    public string OriginatingOn { get; init; } = string.Empty;

    public DateOnly InvoiceDate { get; init; }

    /// <summary>Amount settled. Written as CreditAmount for money received.</summary>
    public decimal Amount { get; init; }
}
