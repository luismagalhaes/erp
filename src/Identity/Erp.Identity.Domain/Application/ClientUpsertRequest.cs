namespace Erp.Identity.Domain.Application;

public sealed record ClientUpsertRequest(
    string ClientId,
    string ClientName,
    bool Enabled,
    bool RequirePkce,
    bool RequireClientSecret,
    bool AllowOfflineAccess,
    int AccessTokenLifetime,
    int SlidingRefreshTokenLifetime,
    int AbsoluteRefreshTokenLifetime,
    string RefreshTokenUsage,
    string RefreshTokenExpiration,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedCorsOrigins,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<ClientSecretItem> ClientSecrets);
