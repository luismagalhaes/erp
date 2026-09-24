namespace Erp.Identity.Domain.Application;

/// <param name="Culture">The inviter's UI culture, so the email reads in a language they chose.</param>
public sealed record InviteToCompanyRequest(
    string Email,
    Guid CompanyId,
    string CompanyName,
    string Role,
    string? InvitedByEmail = null,
    string? Culture = null);
