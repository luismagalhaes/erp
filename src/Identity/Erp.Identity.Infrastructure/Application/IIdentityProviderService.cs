using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface IIdentityProviderService
{
    Task<IReadOnlyList<IdentityProviderListItem>> GetIdentityProvidersAsync(CancellationToken cancellationToken = default);
    Task<IdentityProviderEditItem?> GetIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default);
    Task CreateIdentityProviderAsync(IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateIdentityProviderAsync(string scheme, IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default);
}
