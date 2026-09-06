using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class ApiScopeService(IApiScopeStorage apiScopeStorage) : IApiScopeService
{
    public Task<IReadOnlyList<ApiScopeListItem>> GetApiScopesAsync(CancellationToken cancellationToken = default)
        => apiScopeStorage.GetApiScopesAsync(cancellationToken);

    public Task<ApiScopeEditItem?> GetApiScopeAsync(string name, CancellationToken cancellationToken = default)
        => apiScopeStorage.GetApiScopeAsync(name, cancellationToken);

    public Task CreateApiScopeAsync(ApiScopeUpsertRequest request, CancellationToken cancellationToken = default)
        => apiScopeStorage.CreateApiScopeAsync(request, cancellationToken);

    public Task UpdateApiScopeAsync(string name, ApiScopeUpsertRequest request, CancellationToken cancellationToken = default)
        => apiScopeStorage.UpdateApiScopeAsync(name, request, cancellationToken);

    public Task DeleteApiScopeAsync(string name, CancellationToken cancellationToken = default)
        => apiScopeStorage.DeleteApiScopeAsync(name, cancellationToken);
}
