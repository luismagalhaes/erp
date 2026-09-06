namespace Erp.Identity.Domain.Application;

public sealed record ApiScopeListItem(
    string Name,
    string DisplayName,
    bool Enabled,
    IReadOnlyList<string> UserClaims);
