namespace Erp.Main.Models.Core;

/// <summary>Mirrors Erp.Core's CompanyInvitationDto: someone invited who has not signed up yet.</summary>
public sealed record CompanyInvitation(
    Guid Id,
    string Email,
    string Role,
    string? InvitedByEmail,
    DateTime CreatedAtUtc,
    DateTime LastSentAtUtc);

/// <summary>
/// Mirrors Erp.Core's CompanyMembersDto. <paramref name="InvitationsAvailable"/> is false when the
/// Identity host could not be asked, so an empty list is not read as "nobody was invited".
/// </summary>
public sealed record CompanyMembers(
    IReadOnlyList<UserCompanyAdmin> Members,
    IReadOnlyList<CompanyInvitation> Invitations,
    bool InvitationsAvailable);

public sealed record AddCompanyMemberRequest(string Email, string? Culture = null);

/// <summary>What adding an email to a company led to; see <see cref="AddCompanyMemberOutcomes"/>.</summary>
public sealed record AddCompanyMemberResult(string Outcome, UserCompanyAdmin? Member, CompanyInvitation? Invitation);

public static class AddCompanyMemberOutcomes
{
    /// <summary>The email belongs to an existing account, which now belongs to the company.</summary>
    public const string Added = "Added";

    /// <summary>No account uses the email yet: an invitation was sent and waits for them to sign up.</summary>
    public const string Invited = "Invited";
}

public sealed record ClaimInvitationsResult(int Claimed);
