namespace Erp.Identity.Domain.Application;

public sealed record ClientSecretItem(
    string Value,
    string? Description,
    bool IsHashed);
