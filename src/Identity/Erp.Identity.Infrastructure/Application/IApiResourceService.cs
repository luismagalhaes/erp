using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface IApiResourceService
{
    Task<IReadOnlyList<ApiResourceListItem>> GetApiResourcesAsync(CancellationToken cancellationToken = default);
    Task<ApiResourceEditItem?> GetApiResourceAsync(string name, CancellationToken cancellationToken = default);
    Task CreateApiResourceAsync(ApiResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateApiResourceAsync(string name, ApiResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteApiResourceAsync(string name, CancellationToken cancellationToken = default);
}
