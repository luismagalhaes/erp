using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Storage;

public interface IIdentityResourceStorage
{
    Task<IReadOnlyList<IdentityResourceListItem>> GetIdentityResourcesAsync(CancellationToken cancellationToken = default);
    Task<IdentityResourceEditItem?> GetIdentityResourceAsync(string name, CancellationToken cancellationToken = default);
    Task CreateIdentityResourceAsync(IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateIdentityResourceAsync(string name, IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteIdentityResourceAsync(string name, CancellationToken cancellationToken = default);
}
