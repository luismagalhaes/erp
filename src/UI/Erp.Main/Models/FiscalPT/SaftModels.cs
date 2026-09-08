namespace Erp.Main.Models.FiscalPT;

/// <summary>What a period holds, shown before the file is generated.</summary>
public sealed record SaftPeriodSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    int InvoiceCount,
    int StockMovementCount,
    int PaymentCount,
    decimal InvoiceGrossTotal,
    decimal PaymentGrossTotal);

/// <summary>The generated file, on its way to the browser.</summary>
/// <param name="ValidationErrors">
/// How many problems the official schema found. Zero is what a submittable file looks like.
/// </param>
public sealed record SaftFile(string FileName, byte[] Content, int ValidationErrors);
