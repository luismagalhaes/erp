using System.Security.Claims;
using Erp.Identity.Common.Constants;
using Erp.Identity.Infrastructure.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Identity.Controllers;

/// <summary>
/// The one place a user without any role can change their own. Signing up leaves an account with
/// no role at all — deliberately, since an account that belongs to no company has nothing to be a
/// user of yet. Once the ERP has created their company, it calls back here to finish the job.
/// </summary>
/// <remarks>
/// Deliberately narrow: it only ever grants <see cref="Constants.Roles.User"/>, only ever to the
/// caller named by the token, and never removes or grants anything else. Assigning roles to other
/// accounts, or granting SuperAdmin, stays where it was — behind the backoffice, SuperAdmin only.
/// </remarks>
[ApiController]
[Route("api/self-service")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public sealed class SelfServiceController(
    IUserService userService,
    ILogger<SelfServiceController> logger) : ControllerBase
{
    /// <summary>
    /// Grants the caller the User role, once they have a company. Safe to call again: a caller who
    /// already holds it comes back 204 without anything being written.
    /// </summary>
    [HttpPost("complete-onboarding")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(Constants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "The token carries no user to grant a role to." });

        var user = await userService.GetUserAsync(userId, cancellationToken);

        if (user is null)
            return NotFound(new { error = "The account named by the token no longer exists." });

        if (user.Roles.Contains(Constants.Roles.User, StringComparer.OrdinalIgnoreCase))
            return NoContent();

        // Union, not replace: whatever else the account already holds stays untouched.
        var roles = user.Roles.Append(Constants.Roles.User).ToList();

        await userService.UpdateUserRolesAsync(userId, roles, cancellationToken);

        logger.LogInformation("Granted the {Role} role to {UserId} on completing sign-up.", Constants.Roles.User, userId);

        return NoContent();
    }
}
