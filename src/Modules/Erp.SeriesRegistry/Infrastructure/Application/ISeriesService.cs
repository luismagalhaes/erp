using Erp.SeriesRegistry.Infrastructure.Contracts;

namespace Erp.SeriesRegistry.Infrastructure.Application;

public interface ISeriesService
{
    Task<IReadOnlyList<SeriesListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing as a query, so filtering, sorting and paging can be applied by the database on
    /// behalf of the data grid.
    /// </summary>
    IQueryable<SeriesListItemDto> Query(Guid companyId);

    Task<SeriesListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SeriesListItemDto> CreateAsync(CreateSeriesRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the standard set of series for a company — one per document type, coded
    /// <c>{Tipo}{Ano}</c> and with the usual stock effect for the type.
    /// </summary>
    /// <remarks>
    /// Called when a company is created, so it can be worked with instead of arriving unable to
    /// issue anything. Skips what already exists, so running it twice is harmless.
    /// </remarks>
    Task<IReadOnlyList<SeriesListItemDto>> CreateStandardSetAsync(
        Guid companyId,
        int year,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes what documents of this series do to stock. The only thing about a series that may
    /// change once it exists — see <see cref="UpdateSeriesRequest"/> for why.
    /// </summary>
    Task<SeriesListItemDto?> UpdateAsync(Guid id, UpdateSeriesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the series with the AT webservice and records the validation code it returns,
    /// which unlocks issuing on this series and completes the ATCUD.
    /// </summary>
    Task<SeriesListItemDto?> CommunicateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a validation code obtained outside the webservice — e.g. from the Portal das
    /// Finanças directly — for when the AT webservice is not reachable or not yet configured.
    /// </summary>
    Task<SeriesListItemDto?> CommunicateManuallyAsync(Guid id, string validationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a series communicated by mistake, before any document was issued on it. Not
    /// reversible.
    /// </summary>
    Task<SeriesListItemDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the AT the series is done: valid for the documents already issued, but not to be used
    /// again from here on.
    /// </summary>
    Task<SeriesListItemDto?> FinalizeAsync(Guid id, string? justificacao, CancellationToken cancellationToken = default);
}
