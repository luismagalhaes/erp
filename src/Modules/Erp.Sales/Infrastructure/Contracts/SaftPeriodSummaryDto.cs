namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// What a period holds, so the export screen can show it before the file is generated. Voided
/// documents are counted too: they stay in the file.
/// </summary>
public sealed record SaftPeriodSummaryDto(
    DateOnly StartDate,
    DateOnly EndDate,
    int InvoiceCount,
    int StockMovementCount,
    int PaymentCount,
    decimal InvoiceGrossTotal,
    decimal PaymentGrossTotal);
