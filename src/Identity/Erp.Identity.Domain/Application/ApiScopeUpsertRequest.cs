namespace Erp.Identity.Domain.Application;

public sealed record ApiScopeUpsertRequest(
    string Name,
    string DisplayName,
    string Description,
    bool Enabled,
    bool Required,
    bool Emphasize,
    bool ShowInDiscoveryDocument,
    IReadOnlyList<string> UserClaims);
