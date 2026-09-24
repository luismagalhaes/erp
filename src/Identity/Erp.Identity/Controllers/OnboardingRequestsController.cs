using System.Globalization;
using System.Net;
using Erp.Identity.Common.Constants;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Resources;
using Erp.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Identity.Controllers;

/// <summary>
/// Invitations to join a company, for people who have no account yet. Callable only by the ERP API
/// itself (the <see cref="Constants.Policies.Onboarding"/> policy): it is the authority on whether the
/// caller may invite someone to a company, so a user token is never enough to raise one here.
/// </summary>
[ApiController]
[Route("api/onboarding-requests")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Constants.Policies.Onboarding)]
[Produces("application/json")]
public sealed class OnboardingRequestsController(
    IOnboardingRequestService onboardingRequestService,
    IEmailService emailService,
    IConfiguration configuration,
    IStringLocalizer<IdentityResources> localizer,
    ILogger<OnboardingRequestsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OnboardingRequestListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OnboardingRequestListItem>>> GetAll(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken) =>
        Ok(await onboardingRequestService.GetAllAsync(companyId, cancellationToken));

    /// <summary>
    /// Invites an email address to a company. When an account already uses it the answer carries
    /// that account's id and nothing is sent; otherwise a request is raised and the invitation
    /// email goes out. Inviting the same address to the same company again resends it.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<InviteToCompanyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InviteToCompanyResult>> Invite(
        [FromBody] InviteToCompanyRequest request,
        CancellationToken cancellationToken)
    {
        InviteToCompanyResult result;

        try
        {
            result = await onboardingRequestService.InviteAsync(request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(OnboardingRequestsController), nameof(Invite));
            return BadRequest(new { error = ex.Message });
        }

        if (result.Request is { } onboardingRequest)
            await SendInvitationEmailAsync(onboardingRequest, request.Culture, cancellationToken);

        return Ok(result);
    }

    /// <summary>The pending invitations waiting for this account, once its email is confirmed.</summary>
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<OnboardingRequestListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OnboardingRequestListItem>>> GetPending(
        [FromQuery] string userId,
        CancellationToken cancellationToken) =>
        Ok(await onboardingRequestService.GetPendingForUserAsync(userId, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteOnboardingRequest request, CancellationToken cancellationToken) =>
        await onboardingRequestService.CompleteAsync(id, request.UserId, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        await onboardingRequestService.CancelAsync(id, cancellationToken) ? NoContent() : NotFound();

    private async Task SendInvitationEmailAsync(
        OnboardingRequestListItem request,
        string? culture,
        CancellationToken cancellationToken)
    {
        var previous = CultureInfo.CurrentUICulture;

        // The inviter's language, not the invitee's (nobody knows theirs yet, they have no account).
        if (culture is not null && Constants.Localization.SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            var signUpLink = EmailLinkBuilder.Build(
                configuration,
                "/Account/SignUp",
                new Dictionary<string, string?> { ["email"] = request.Email });

            var inviter = string.IsNullOrWhiteSpace(request.InvitedByEmail)
                ? localizer["Onboarding_Email_InviterFallback"].Value
                : request.InvitedByEmail;

            // Both come from another service or a person, so they are encoded before they reach HTML.
            var body = string.Format(
                CultureInfo.CurrentUICulture,
                localizer["Onboarding_Email_Body"].Value,
                WebUtility.HtmlEncode(inviter),
                WebUtility.HtmlEncode(request.CompanyName));

            var html = $"<p>{localizer["Onboarding_Email_Greeting"]}</p><p>{body}</p>"
                + $"<p><a href=\"{WebUtility.HtmlEncode(signUpLink)}\">{localizer["Onboarding_Email_Link"]}</a></p>";

            var subject = string.Format(
                CultureInfo.CurrentUICulture, localizer["Onboarding_Email_Subject"].Value, request.CompanyName);

            await emailService.SendAsync(request.Email, subject, html, cancellationToken);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
