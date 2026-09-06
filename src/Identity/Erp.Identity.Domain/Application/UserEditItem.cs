namespace Erp.Identity.Domain.Application;

public sealed record UserEditItem(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles);
