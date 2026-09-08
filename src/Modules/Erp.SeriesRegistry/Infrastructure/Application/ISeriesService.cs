using Erp.SeriesRegistry.Infrastructure.Contracts;

namespace Erp.SeriesRegistry.Infrastructure.Application;

public interface ISeriesService
{
    Task<IReadOnlyList<SeriesListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

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
    /// Records the validation code returned by the tax authority, which unlocks issuing on
    /// this series and completes the ATCUD.
    /// </summary>
    Task<SeriesListItemDto?> CommunicateAsync(Guid id, string validationCode, CancellationToken cancellationToken = default);
}
