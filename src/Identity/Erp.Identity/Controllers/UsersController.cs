using Erp.Identity.Common.Constants;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Identity.Controllers;

/// <summary>
/// Read only view of the user store for other ERP applications. The users live here, so the
/// backoffice that assigns them to companies reads them from this endpoint instead of keeping
/// its own copy.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = $"{Constants.Roles.Admin},{Constants.Roles.SuperAdmin}")]
[Produces("application/json")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lists every user with the roles they hold.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserListItem>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await userService.GetUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>Gets a single user.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<UserEditItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserEditItem>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }
}
