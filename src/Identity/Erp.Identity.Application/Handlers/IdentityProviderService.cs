using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class IdentityProviderService(IIdentityProviderStorage identityProviderStorage) : IIdentityProviderService
{
    public Task<IReadOnlyList<IdentityProviderListItem>> GetIdentityProvidersAsync(CancellationToken cancellationToken = default)
        => identityProviderStorage.GetIdentityProvidersAsync(cancellationToken);

    public Task<IdentityProviderEditItem?> GetIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default)
        => identityProviderStorage.GetIdentityProviderAsync(scheme, cancellationToken);

    public Task CreateIdentityProviderAsync(IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default)
        => identityProviderStorage.CreateIdentityProviderAsync(request, cancellationToken);

    public Task UpdateIdentityProviderAsync(string scheme, IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default)
        => identityProviderStorage.UpdateIdentityProviderAsync(scheme, request, cancellationToken);

    public Task DeleteIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default)
        => identityProviderStorage.DeleteIdentityProviderAsync(scheme, cancellationToken);
}
