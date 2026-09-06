using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class RoleService(IRoleStorage roleStorage) : IRoleService
{
    public Task<IReadOnlyList<RoleListItem>> GetRolesAsync(CancellationToken cancellationToken = default)
        => roleStorage.GetRolesAsync(cancellationToken);

    public Task<RoleEditItem?> GetRoleAsync(string roleName, CancellationToken cancellationToken = default)
        => roleStorage.GetRoleAsync(roleName, cancellationToken);

    public Task CreateRoleAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default)
        => roleStorage.CreateRoleAsync(request, cancellationToken);

    public Task UpdateRoleAsync(string roleName, RoleUpsertRequest request, CancellationToken cancellationToken = default)
        => roleStorage.UpdateRoleAsync(roleName, request, cancellationToken);

    public Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
        => roleStorage.DeleteRoleAsync(roleName, cancellationToken);
}
