namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// What a company sold in one month. Credit notes are taken off and voided documents left out, so
/// the totals are net sales, not the sum of everything issued.
/// </summary>
/// <param name="NetTotal">Without VAT.</param>
/// <param name="GrossTotal">With VAT.</param>
/// <param name="DocumentCount">Documents that count towards the total, credit notes included.</param>
public sealed record MonthlySalesDto(
    int Year,
    int Month,
    decimal NetTotal,
    decimal GrossTotal,
    int DocumentCount);
