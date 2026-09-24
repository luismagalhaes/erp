namespace Erp.Core.Infrastructure.Contracts;

/// <param name="Culture">The caller's UI culture, so an invitation email goes out in a language they chose.</param>
public sealed record AddCompanyMemberRequest(string Email, string? Culture = null);

/// <summary>An invitation waiting for someone with no account yet to sign up.</summary>
public sealed record CompanyInvitationDto(
    Guid Id,
    string Email,
    string Role,
    string? InvitedByEmail,
    DateTime CreatedAtUtc,
    DateTime LastSentAtUtc);

/// <summary>
/// The people of a company: who belongs to it, and who has been invited but has no account yet.
/// <paramref name="InvitationsAvailable"/> is false when the Identity host could not be asked, so
/// an empty list is not mistaken for "nobody was invited".
/// </summary>
public sealed record CompanyMembersDto(
    IReadOnlyList<UserCompanyAdminDto> Members,
    IReadOnlyList<CompanyInvitationDto> Invitations,
    bool InvitationsAvailable);

/// <summary>How many waiting invitations were turned into memberships when the user opened the application.</summary>
public sealed record ClaimInvitationsResult(int Claimed);

/// <summary>What adding an email address to a company led to. See <see cref="AddCompanyMemberOutcomes"/>.</summary>
public sealed record AddCompanyMemberResult(
    string Outcome,
    UserCompanyAdminDto? Member,
    CompanyInvitationDto? Invitation);
