namespace Erp.Identity.Domain.Application;

public sealed record ApiScopeEditItem(
    string Name,
    string DisplayName,
    string Description,
    bool Enabled,
    bool Required,
    bool Emphasize,
    bool ShowInDiscoveryDocument,
    IReadOnlyList<string> UserClaims);
