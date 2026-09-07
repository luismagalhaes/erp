namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>The generated file plus the counts a user needs to see before submitting it.</summary>
/// <param name="ValidationErrors">
/// What the official schema rejected, empty when the file conforms. The file is returned either
/// way: seeing what is wrong beats being told nothing at all.
/// </param>
public sealed record SaftExportResult(
    string FileName,
    byte[] Content,
    int InvoiceCount,
    int StockMovementCount,
    int PaymentCount,
    IReadOnlyList<string> ValidationErrors);
