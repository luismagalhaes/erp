using Erp.FiscalPT.Documents;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

public sealed class SeriesService(ISeriesStorage storage, ISalesUnitOfWork unitOfWork) : ISeriesService
{
    public async Task<IReadOnlyList<SeriesListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var series = await storage.GetAllAsync(companyId, cancellationToken);
        return series.Select(Map).ToList();
    }

    public async Task<SeriesListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await storage.GetByIdAsync(id, cancellationToken);
        return series is null ? null : Map(series);
    }

    public async Task<SeriesListItemDto> CreateAsync(CreateSeriesRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A series numbers either invoicing documents or movement documents; both are numbered
        // and signed the same way.
        if (!SalesDocumentTypes.IsSupported(request.DocumentType)
            && !MovementDocumentTypes.IsSupported(request.DocumentType))
        {
            throw new ArgumentException($"Unsupported document type '{request.DocumentType}'.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SeriesCode))
            throw new ArgumentException("Series code is required.", nameof(request));

        if (await storage.ExistsAsync(request.CompanyId, request.DocumentType, request.SeriesCode, cancellationToken))
            throw new InvalidOperationException($"Series '{request.SeriesCode}' already exists for document type '{request.DocumentType}'.");

        var series = new Series
        {
            CompanyId = request.CompanyId,
            DocumentType = request.DocumentType,
            SeriesCode = request.SeriesCode.Trim(),
            InitialSequence = request.InitialSequence < 1 ? 1 : request.InitialSequence,
            EstablishmentCode = request.EstablishmentCode,
            CreatedByUserId = userId
        };

        await storage.AddAsync(series, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(series);
    }

    public async Task<SeriesListItemDto?> CommunicateAsync(Guid id, string validationCode, CancellationToken cancellationToken = default)
    {
        var series = await storage.GetByIdAsync(id, cancellationToken);
        if (series is null)
            return null;

        series.Communicate(validationCode.Trim(), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(series);
    }

    private static SeriesListItemDto Map(Series series) =>
        new(series.Id,
            series.CompanyId,
            series.DocumentType,
            series.SeriesCode,
            series.CurrentSequence,
            series.ValidationCode,
            series.Status.ToString(),
            series.CanIssue);
}
