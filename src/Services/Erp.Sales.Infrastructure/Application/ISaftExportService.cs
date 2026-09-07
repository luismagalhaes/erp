using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface ISaftExportService
{
    /// <summary>What a period holds, without generating the file.</summary>
    Task<SaftPeriodSummaryDto> GetSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the SAF-T (PT) file for the period: header, master files derived from the
    /// documents, and the three families of source documents.
    /// </summary>
    Task<SaftExportResult> ExportAsync(SaftExportRequest request, CancellationToken cancellationToken = default);
}
