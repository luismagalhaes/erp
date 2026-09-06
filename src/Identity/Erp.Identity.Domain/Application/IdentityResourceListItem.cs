namespace Erp.Identity.Domain.Application;

public sealed record IdentityResourceListItem(
    string Name,
    string DisplayName,
    bool Enabled,
    IReadOnlyList<string> UserClaims);
