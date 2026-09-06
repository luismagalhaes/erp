namespace Erp.Identity.Domain.Application;

public sealed record IdentityProviderListItem(
    string Scheme,
    string DisplayName,
    string Type,
    bool Enabled);
