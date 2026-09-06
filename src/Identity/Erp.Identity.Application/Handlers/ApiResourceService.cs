using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class ApiResourceService(IApiResourceStorage apiResourceStorage) : IApiResourceService
{
    public Task<IReadOnlyList<ApiResourceListItem>> GetApiResourcesAsync(CancellationToken cancellationToken = default)
        => apiResourceStorage.GetApiResourcesAsync(cancellationToken);

    public Task<ApiResourceEditItem?> GetApiResourceAsync(string name, CancellationToken cancellationToken = default)
        => apiResourceStorage.GetApiResourceAsync(name, cancellationToken);

    public Task CreateApiResourceAsync(ApiResourceUpsertRequest request, CancellationToken cancellationToken = default)
        => apiResourceStorage.CreateApiResourceAsync(request, cancellationToken);

    public Task UpdateApiResourceAsync(string name, ApiResourceUpsertRequest request, CancellationToken cancellationToken = default)
        => apiResourceStorage.UpdateApiResourceAsync(name, request, cancellationToken);

    public Task DeleteApiResourceAsync(string name, CancellationToken cancellationToken = default)
        => apiResourceStorage.DeleteApiResourceAsync(name, cancellationToken);
}
