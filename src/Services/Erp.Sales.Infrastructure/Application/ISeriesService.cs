using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface ISeriesService
{
    Task<IReadOnlyList<SeriesListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<SeriesListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SeriesListItemDto> CreateAsync(CreateSeriesRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the validation code returned by the tax authority, which unlocks issuing on
    /// this series and completes the ATCUD.
    /// </summary>
    Task<SeriesListItemDto?> CommunicateAsync(Guid id, string validationCode, CancellationToken cancellationToken = default);
}
