using Erp.Api.Authorization;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>Second level of the product classification, always inside a family.</summary>
[ApiController]
[Route("api/product-subfamilies")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class ProductSubfamiliesController(IProductSubfamilyService subfamilyService) : ControllerBase
{
    /// <summary>
    /// Lists the subfamilies of a company, optionally limited to one family.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductSubfamilyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductSubfamilyDto>>> GetAll(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? familyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await subfamilyService.GetAllAsync(companyId, familyId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductSubfamilyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductSubfamilyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await subfamilyService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<ProductSubfamilyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductSubfamilyDto>> Create(
        [FromBody] CreateProductSubfamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await subfamilyService.CreateAsync(request, cancellationToken);
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
    [ProducesResponseType<ProductSubfamilyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductSubfamilyDto>> Update(
        Guid id,
        [FromBody] UpdateProductSubfamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await subfamilyService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
