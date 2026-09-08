using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

/// <summary>
/// What a period holds, so the user can look before generating the file.
/// </summary>
/// <remarks>
/// This is all that is left here of the SAF-T. Producing the file is the
/// <c>SaftExporter</c>'s job, in <c>Erp.FiscalPT</c>, because a SAF-T belongs to the taxable entity
/// and not to the sales module. What stays is the summary, which really is Sales data: how many
/// documents this module issued in the period, and what they were worth.
/// </remarks>
public sealed class SaftSummaryService(
    ISalesDocumentStorage documentStorage,
    IStockMovementStorage movementStorage,
    IPaymentStorage paymentStorage) : ISaftSummaryService
{
    public async Task<SaftPeriodSummaryDto> GetSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("The end date is before the start date.", nameof(endDate));

        var documents = await documentStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);
        var movements = await movementStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);
        var payments = await paymentStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);

        return new SaftPeriodSummaryDto(
            startDate,
            endDate,
            documents.Count,
            movements.Count,
            payments.Count,
            documents.Where(x => !x.IsVoided).Sum(x => x.GrossTotal),
            payments.Where(x => !x.IsVoided).Sum(x => x.GrossTotal));
    }
}
