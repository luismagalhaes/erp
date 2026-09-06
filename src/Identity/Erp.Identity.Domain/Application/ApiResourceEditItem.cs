namespace Erp.Identity.Domain.Application;

public sealed record ApiResourceEditItem(
    string Name,
    string DisplayName,
    string Description,
    bool Enabled,
    bool ShowInDiscoveryDocument,
    bool RequireResourceIndicator,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> UserClaims);
