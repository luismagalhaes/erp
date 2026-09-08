namespace Erp.FiscalPT.Saft;

/// <param name="ValidationErrors">
/// What the official schema rejected, empty when the file conforms. The file is returned either
/// way — a broken file in hand beats a silent refusal — so the caller must not ignore this.
/// </param>
public sealed record SaftExportOutcome(
    string FileName,
    byte[] Content,
    int InvoiceCount,
    int StockMovementCount,
    int PaymentCount,
    IReadOnlyList<string> ValidationErrors);
