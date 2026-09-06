using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface IApiScopeService
{
    Task<IReadOnlyList<ApiScopeListItem>> GetApiScopesAsync(CancellationToken cancellationToken = default);
    Task<ApiScopeEditItem?> GetApiScopeAsync(string name, CancellationToken cancellationToken = default);
    Task CreateApiScopeAsync(ApiScopeUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateApiScopeAsync(string name, ApiScopeUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteApiScopeAsync(string name, CancellationToken cancellationToken = default);
}
