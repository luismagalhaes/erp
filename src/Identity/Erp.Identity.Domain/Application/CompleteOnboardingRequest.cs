namespace Erp.Identity.Domain.Application;

/// <summary>The account that took up an invitation.</summary>
public sealed record CompleteOnboardingRequest(string UserId);
