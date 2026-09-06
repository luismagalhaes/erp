namespace Erp.Identity.Domain.Application;

public sealed record ClientListItem(
    string ClientId,
    string ClientName,
    bool Enabled,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> AllowedScopes);
