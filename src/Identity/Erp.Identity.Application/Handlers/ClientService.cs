using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class ClientService(IClientStorage clientStorage) : IClientService
{
    public Task<IReadOnlyList<ClientListItem>> GetClientsAsync(CancellationToken cancellationToken = default)
        => clientStorage.GetClientsAsync(cancellationToken);

    public Task<ClientEditItem?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
        => clientStorage.GetClientAsync(clientId, cancellationToken);

    public Task CreateClientAsync(ClientUpsertRequest request, CancellationToken cancellationToken = default)
        => clientStorage.CreateClientAsync(request, cancellationToken);

    public Task UpdateClientAsync(string clientId, ClientUpsertRequest request, CancellationToken cancellationToken = default)
        => clientStorage.UpdateClientAsync(clientId, request, cancellationToken);

    public Task DeleteClientAsync(string clientId, CancellationToken cancellationToken = default)
        => clientStorage.DeleteClientAsync(clientId, cancellationToken);
}
