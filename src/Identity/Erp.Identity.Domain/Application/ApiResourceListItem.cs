namespace Erp.Identity.Domain.Application;

public sealed record ApiResourceListItem(
    string Name,
    string DisplayName,
    bool Enabled,
    IReadOnlyList<string> Scopes);
