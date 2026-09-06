using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class IdentityResourceService(IIdentityResourceStorage identityResourceStorage) : IIdentityResourceService
{
    public Task<IReadOnlyList<IdentityResourceListItem>> GetIdentityResourcesAsync(CancellationToken cancellationToken = default)
        => identityResourceStorage.GetIdentityResourcesAsync(cancellationToken);

    public Task<IdentityResourceEditItem?> GetIdentityResourceAsync(string name, CancellationToken cancellationToken = default)
        => identityResourceStorage.GetIdentityResourceAsync(name, cancellationToken);

    public Task CreateIdentityResourceAsync(IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default)
        => identityResourceStorage.CreateIdentityResourceAsync(request, cancellationToken);

    public Task UpdateIdentityResourceAsync(string name, IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default)
        => identityResourceStorage.UpdateIdentityResourceAsync(name, request, cancellationToken);

    public Task DeleteIdentityResourceAsync(string name, CancellationToken cancellationToken = default)
        => identityResourceStorage.DeleteIdentityResourceAsync(name, cancellationToken);
}
