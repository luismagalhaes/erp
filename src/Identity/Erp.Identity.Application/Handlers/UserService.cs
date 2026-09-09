using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class UserService(IUserStorage userStorage) : IUserService
{
    public Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
        => userStorage.GetUsersAsync(cancellationToken);

    public Task<UserEditItem?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
        => userStorage.GetUserAsync(userId, cancellationToken);

    public Task UpdateUserRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
        => userStorage.UpdateUserRolesAsync(userId, roles, cancellationToken);

    public Task UpdateUserLanguageAsync(string userId, string preferredLanguage, CancellationToken cancellationToken = default)
        => userStorage.UpdateUserLanguageAsync(userId, preferredLanguage, cancellationToken);
}
