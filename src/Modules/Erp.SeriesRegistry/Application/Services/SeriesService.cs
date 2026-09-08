using Erp.Common;
using Erp.FiscalPT.Documents;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.SeriesRegistry.Application.Services;

public sealed class SeriesService(ISeriesStorage storage, IUnitOfWork unitOfWork) : ISeriesService
{
    public async Task<IReadOnlyList<SeriesListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var series = await storage.GetAllAsync(companyId, cancellationToken);
        return series.Select(Map).ToList();
    }

    public IQueryable<SeriesListItemDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<SeriesListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var series = await storage.GetByIdAsync(id, cancellationToken);
        return series is null ? null : Map(series);
    }

    public async Task<SeriesListItemDto> CreateAsync(CreateSeriesRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A series numbers invoicing documents, movement documents or receipts; all three are
        // numbered and signed the same way.
        if (!SalesDocumentTypes.IsSupported(request.DocumentType)
            && !MovementDocumentTypes.IsSupported(request.DocumentType)
            && !PaymentDocumentTypes.IsSupported(request.DocumentType))
        {
            throw new ArgumentException($"Unsupported document type '{request.DocumentType}'.", nameof(request));
        }

        // Self-billing is a way of invoicing, so it only makes sense on an invoicing type. A
        // self-billed guia or recibo is not a thing the regime provides for.
        if (request.SelfBilling && !SalesDocumentTypes.IsSupported(request.DocumentType))
        {
            throw new ArgumentException(
                $"Document type '{request.DocumentType}' cannot be self-billed.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SeriesCode))
            throw new ArgumentException("Series code is required.", nameof(request));

        if (await storage.ExistsAsync(request.CompanyId, request.DocumentType, request.SeriesCode, cancellationToken))
            throw new InvalidOperationException($"Series '{request.SeriesCode}' already exists for document type '{request.DocumentType}'.");

        var series = new Domain.Series
        {
            CompanyId = request.CompanyId,
            DocumentType = request.DocumentType,
            SeriesCode = request.SeriesCode.Trim(),
            InitialSequence = request.InitialSequence < 1 ? 1 : request.InitialSequence,
            EstablishmentCode = request.EstablishmentCode,
            // The document type says what usually happens to stock; the caller can override it,
            // because the same type is used differently by different businesses.
            StockEffect = ParseStockEffect(request.StockEffect, request.DocumentType),
            SelfBilling = request.SelfBilling,
            CreatedByUserId = userId
        };

        await storage.AddAsync(series, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(series);
    }

    public async Task<IReadOnlyList<SeriesListItemDto>> CreateStandardSetAsync(
        Guid companyId,
        int year,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        // One reading of what is already there, rather than one per document type.
        var existing = await storage.GetAllAsync(companyId, cancellationToken);

        var taken = existing
            .Select(series => (series.DocumentType, series.SeriesCode))
            .ToHashSet();

        var created = new List<Domain.Series>();

        foreach (var documentType in StandardSeries.DocumentTypes)
        {
            var seriesCode = StandardSeries.CodeFor(documentType, year);

            // Already set up — by a previous run, or by hand. Leave it alone: the numbers issued
            // from it are the whole reason it cannot simply be replaced.
            if (!taken.Add((documentType, seriesCode)))
                continue;

            var series = new Domain.Series
            {
                CompanyId = companyId,
                DocumentType = documentType,
                SeriesCode = seriesCode,
                InitialSequence = 1,
                StockEffect = DefaultStockEffects.For(documentType),
                CreatedByUserId = userId
            };

            await storage.AddAsync(series, cancellationToken);
            created.Add(series);
        }

        if (created.Count > 0)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return [.. created.Select(Map)];
    }

    public async Task<SeriesListItemDto?> UpdateAsync(
        Guid id,
        UpdateSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // On a create, saying nothing means "use the default for the type". On an update it means
        // the caller sent an empty field, and silently resetting the effect is not what they meant.
        if (string.IsNullOrWhiteSpace(request.StockEffect))
            throw new ArgumentException("A stock effect is required.", nameof(request));

        var series = await storage.GetByIdAsync(id, cancellationToken);
        if (series is null)
            return null;

        // Everything else about a series is either communicated to the tax authority or already
        // written into documents, so the stock effect is all that is offered.
        series.StockEffect = ParseStockEffect(request.StockEffect, series.DocumentType);

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

    /// <summary>
    /// Takes the effect the caller asked for, or the default for the document type when it said
    /// nothing. An unknown value is refused rather than quietly turned into "moves no stock",
    /// which would leave the warehouse wrong without anyone noticing.
    /// </summary>
    private static StockEffect ParseStockEffect(string? requested, string documentType)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return DefaultStockEffects.For(documentType);

        if (!Enum.TryParse<StockEffect>(requested, ignoreCase: true, out var effect))
            throw new ArgumentException($"Unknown stock effect '{requested}'.", nameof(requested));

        return effect;
    }

    private static SeriesListItemDto Map(Domain.Series series) =>
        new()
        {
            Id = series.Id,
            CompanyId = series.CompanyId,
            DocumentType = series.DocumentType,
            SeriesCode = series.SeriesCode,
            CurrentSequence = series.CurrentSequence,
            ValidationCode = series.ValidationCode,
            Status = series.Status.ToString(),
            CanIssue = series.CanIssue,
            StockEffect = series.StockEffect.ToString(),
            SelfBilling = series.SelfBilling
        };
}
