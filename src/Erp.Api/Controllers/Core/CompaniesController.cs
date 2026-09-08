using System.Security.Claims;
using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CompaniesController(
    ICompanyAdminService companyAdminService,
    ISeriesService seriesService,
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
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
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

    /// <summary>Updates a company.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CompanyDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetailDto>> Update(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await companyAdminService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
