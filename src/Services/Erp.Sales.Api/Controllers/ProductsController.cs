using Erp.Sales.Api.Authorization;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Sales.Api.Controllers;

/// <summary>Product master file used to fill document lines and the SAF-T Product table.</summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    /// <summary>Lists the products of a company.</summary>
    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType<IReadOnlyList<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var result = await productService.GetAllAsync(companyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a single product.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType<ProductListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductListItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Creates a product.</summary>
    [HttpPost]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType<ProductListItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductListItemDto>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await productService.CreateAsync(request, cancellationToken);
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

    /// <summary>Updates a product. Documents already issued keep their own copy of the old values.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType<ProductListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductListItemDto>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await productService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
