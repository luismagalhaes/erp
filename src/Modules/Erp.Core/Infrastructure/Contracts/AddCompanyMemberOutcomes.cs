namespace Erp.Core.Infrastructure.Contracts;

public static class AddCompanyMemberOutcomes
{
    /// <summary>The email belongs to an existing account, which now belongs to the company.</summary>
    public const string Added = "Added";

    /// <summary>No account uses the email yet: an invitation was sent and waits for them to sign up.</summary>
    public const string Invited = "Invited";
}
