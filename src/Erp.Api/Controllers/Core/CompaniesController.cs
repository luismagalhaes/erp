using System.Security.Claims;
using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CompaniesController(
    ICompanyAdminService companyAdminService,
    ISeriesService seriesService,
    ICompanyAtCredentialService companyAtCredentialService,
    DemoDataService demoDataService,
    IUserCompanyService userCompanyService,
    IOnboardingService onboardingService,
    ILogger<CompaniesController> logger) : ControllerBase
{
    /// <summary>Lists every company.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await companyAdminService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The listing the backoffice data grid calls. Filtering, sorting and paging travel as OData
    /// options and are applied by the database. Companies are a global listing, so there is no
    /// tenancy filter to keep outside the query here.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType<ODataCollection<CompanyListItemDto>>(StatusCodes.Status200OK)]
    public ActionResult<ODataCollection<CompanyListItemDto>> Query(ODataQueryOptions<CompanyListItemDto> options) =>
        Ok(ODataQueryExecutor.Execute(companyAdminService.Query(), options));

    /// <summary>Gets a single company.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await companyAdminService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Creates a company, together with the standard set of document series for the current year.
    /// </summary>
    /// <remarks>
    /// The series are seeded here, by the host, rather than by the Core module: a company knows
    /// nothing about document series, and the series registry knows nothing about companies. The
    /// two are brought together in the one place that is allowed to know both.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CompanyDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetailDto>> Create(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await companyAdminService.CreateAsync(request, cancellationToken);

            await SeedSeriesAsync(created, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompaniesController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompaniesController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Sign-up: creates a company for the caller and makes them its owner, with the subscription
    /// package they picked. This is the one company-creating path that is not SuperAdmin-only —
    /// deliberately, because a user who has just registered has nobody to ask. It is narrow on
    /// purpose: the user is taken from the token rather than the body, and it refuses outright once
    /// the caller already belongs to a company.
    /// </summary>
    [HttpPost("self-service")]
    [ProducesResponseType<CompanyDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetailDto>> CreateForCurrentUser(
        [FromBody] SelfServiceCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "The token carries no user to create a company for." });

        try
        {
            var created = await onboardingService.CreateCompanyForUserAsync(userId, request, cancellationToken);

            await SeedSeriesAsync(created, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompaniesController), nameof(CreateForCurrentUser));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompaniesController), nameof(CreateForCurrentUser));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Whether the caller still has to go through sign-up, i.e. belongs to no company yet.</summary>
    [HttpGet("self-service/status")]
    [ProducesResponseType<OnboardingStatus>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStatus>> GetOnboardingStatus(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var hasCompany = !string.IsNullOrWhiteSpace(userId)
            && await onboardingService.HasCompanyAsync(userId, cancellationToken);

        return Ok(new OnboardingStatus(hasCompany));
    }

    /// <summary>
    /// Gives the new company its standard series. A failure here is logged and swallowed: the
    /// company was created and telling the caller otherwise would be a lie, and the series can be
    /// created by hand or by creating the company's series again later.
    /// </summary>
    private async Task SeedSeriesAsync(CompanyDetailDto company, CancellationToken cancellationToken)
    {
        try
        {
            var series = await seriesService.CreateStandardSetAsync(
                company.Id, DateTime.Today.Year, GetCurrentUserId(), cancellationToken);

            logger.LogInformation(
                "Created {Count} standard series for company {CompanyName}.", series.Count, company.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Company {CompanyName} was created but its standard series were not.", company.Name);
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(Constants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>A SuperAdmin, or a member of the company: the same reach <see cref="Update"/> grants.</summary>
    private async Task<bool> CanManageCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(Constants.Roles.SuperAdmin))
            return true;

        var userId = GetCurrentUserId();
        return !string.IsNullOrWhiteSpace(userId)
            && await userCompanyService.CanAccessCompanyAsync(userId, companyId, cancellationToken);
    }

    /// <summary>
    /// Updates a company. A SuperAdmin can update any company; a regular user can only update the
    /// company they belong to \u2014 they cannot create a new one, that stays SuperAdmin-only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<CompanyDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetailDto>> Update(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Constants.Roles.SuperAdmin))
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId)
                || !await userCompanyService.CanAccessCompanyAsync(userId, id, cancellationToken))
            {
                return Forbid();
            }
        }

        try
        {
            var updated = await companyAdminService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompaniesController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompaniesController), nameof(Update));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Whether the company has a WDT subutilizador registered for AT transport-document
    /// communication, and which one — never the password.
    /// </summary>
    [HttpGet("{id:guid}/at-credentials")]
    [ProducesResponseType<CompanyAtCredentialStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyAtCredentialStatus>> GetAtCredentialStatus(
        Guid id, CancellationToken cancellationToken)
    {
        if (!await CanManageCompanyAsync(id, cancellationToken))
            return Forbid();

        var status = await companyAtCredentialService.GetStatusAsync(id, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// Registers or replaces the company's WDT subutilizador. Write-only: the password is never
    /// returned by this or any other endpoint once saved. A SuperAdmin or any member of the company
    /// may set it: they are the ones who hold the credential at the Portal das Finanças.
    /// </summary>
    [HttpPut("{id:guid}/at-credentials")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAtCredentials(
        Guid id, [FromBody] SetCompanyAtCredentialsRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageCompanyAsync(id, cancellationToken))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request?.SubUserId) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "SubUserId and Password are required." });

        try
        {
            await companyAtCredentialService.SetCredentialsAsync(id, request.SubUserId, request.Password, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompaniesController), nameof(SetAtCredentials));
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Seeds the company with realistic auto-parts demo data — families, subfamilies, brands,
    /// products — and communicates its standard document series with a fake validation code, so
    /// the company looks and behaves like a working one for demonstrations. Safe to call more than
    /// once: it does nothing once demo data is already there.
    /// </summary>
    [HttpPost("{id:guid}/apply-demo-data")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DemoDataResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DemoDataResult>> ApplyDemoData(Guid id, CancellationToken cancellationToken)
    {
        var company = await companyAdminService.GetByIdAsync(id, cancellationToken);
        if (company is null)
            return NotFound();

        try
        {
            var result = await demoDataService.ApplyAsync(id, GetCurrentUserId(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Controller}.{Method} failed to apply demo data to company {CompanyId}.", nameof(CompaniesController), nameof(ApplyDemoData), id);
            return Problem(ex.Message);
        }
    }
}
