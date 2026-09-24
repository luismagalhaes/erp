using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Storage;

public interface IUserStorage
{
    Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserEditItem?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>The account using this email, matched case-insensitively, or null.</summary>
    Task<UserEditItem?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task UpdateUserLanguageAsync(string userId, string preferredLanguage, CancellationToken cancellationToken = default);
}
