using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

/// <summary>
/// What this module issued in a period. Producing the SAF-T file itself belongs to the
/// <c>SaftExporter</c> in <c>Erp.FiscalPT</c>: the file is the taxable entity's, not the module's.
/// </summary>
public interface ISaftSummaryService
{
    Task<SaftPeriodSummaryDto> GetSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
