namespace Erp.Identity.Domain.Application;

public sealed record IdentityProviderUpsertRequest(
    string Scheme,
    string DisplayName,
    string Type,
    bool Enabled);
