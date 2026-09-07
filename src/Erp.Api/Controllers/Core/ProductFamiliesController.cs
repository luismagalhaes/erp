using Erp.Api.Authorization;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>Top level product classification.</summary>
[ApiController]
[Route("api/product-families")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class ProductFamiliesController(IProductFamilyService familyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductFamilyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductFamilyDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await familyService.GetAllAsync(companyId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductFamilyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductFamilyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await familyService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<ProductFamilyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductFamilyDto>> Create(
        [FromBody] CreateProductFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await familyService.CreateAsync(request, cancellationToken);
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

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<ProductFamilyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductFamilyDto>> Update(
        Guid id,
        [FromBody] UpdateProductFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await familyService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

