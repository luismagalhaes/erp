using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// A company managing its own people: who belongs to it, adding someone by email, removing them.
/// Any member of the company may use it — <see cref="Security.RequireCompanyAccessFilter"/> checks the
/// caller belongs to the <c>companyId</c> in the route — unlike <c>api/user-companies</c>, which is the
/// SuperAdmin's view across every company.
/// </summary>
[ApiController]
[Route("api/companies/{companyId:guid}/members")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CompanyMembersController(
    ICompanyMemberService companyMemberService,
    ILogger<CompanyMembersController> logger) : ControllerBase
{
    /// <summary>The company's members and the invitations still waiting for someone to sign up.</summary>
    [HttpGet]
    [ProducesResponseType<CompanyMembersDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyMembersDto>> Get(Guid companyId, CancellationToken cancellationToken) =>
        Ok(await companyMemberService.GetAsync(companyId, cancellationToken));

    /// <summary>
    /// Adds a person by email. An existing account is associated to the company straight away; for
    /// an email nobody has signed up with yet, an invitation is sent and it waits for them.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<AddCompanyMemberResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<AddCompanyMemberResult>> Add(
        Guid companyId,
        [FromBody] AddCompanyMemberRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await companyMemberService.AddAsync(companyId, request, User.Identity?.Name, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompanyMembersController), nameof(Add));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompanyMembersController), nameof(Add));
            return Conflict(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            // The Identity host is the one place that knows whether the email has an account.
            logger.LogError(ex, "{Controller}.{Method} could not reach the Identity host.", nameof(CompanyMembersController), nameof(Add));
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The Identity service is not reachable right now. Try again in a moment." });
        }
    }

    [HttpDelete("{membershipId:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid companyId, Guid membershipId, CancellationToken cancellationToken)
    {
        try
        {
            return await companyMemberService.RemoveAsync(companyId, membershipId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompanyMembersController), nameof(Remove));
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("invitations/{invitationId:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CancelInvitation(Guid companyId, Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            return await companyMemberService.CancelInvitationAsync(companyId, invitationId, cancellationToken) ? NoContent() : NotFound();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "{Controller}.{Method} could not reach the Identity host.", nameof(CompanyMembersController), nameof(CancelInvitation));
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The Identity service is not reachable right now. Try again in a moment." });
        }
    }
}
