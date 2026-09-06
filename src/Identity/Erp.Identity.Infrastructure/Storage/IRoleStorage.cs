using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Storage;

public interface IRoleStorage
{
    Task<IReadOnlyList<RoleListItem>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleEditItem?> GetRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task CreateRoleAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(string roleName, RoleUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default);
}
