using Erp.Api.Authorization;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CompaniesController(ICompanyAdminService companyAdminService) : ControllerBase
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

    /// <summary>Creates a company.</summary>
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
