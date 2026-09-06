using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface IClientService
{
    Task<IReadOnlyList<ClientListItem>> GetClientsAsync(CancellationToken cancellationToken = default);
    Task<ClientEditItem?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);
    Task CreateClientAsync(ClientUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateClientAsync(string clientId, ClientUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteClientAsync(string clientId, CancellationToken cancellationToken = default);
}
