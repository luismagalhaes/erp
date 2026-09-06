namespace Erp.Identity.Domain.Application;

public sealed record IdentityProviderEditItem(
    string Scheme,
    string DisplayName,
    string Type,
    bool Enabled);
