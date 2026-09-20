namespace Erp.Identity.Domain.Application;

public sealed record UserListItem(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles,
    string PreferredLanguage);
