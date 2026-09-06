using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Storage;

public interface IUserStorage
{
    Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserEditItem?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
