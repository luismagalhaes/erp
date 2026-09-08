using Erp.Api.Authorization;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// Product master file. Shared by every module: Sales invoices it, Purchasing buys it and
/// Inventory reports it, so it lives in Core and not in any one of them.
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    private const int MaxPageSize = 500;

    /// <summary>Lists the products of a company.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await productService.GetAllAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// Lists the products of a company as an OData query, so the client grids can filter, sort and
    /// page against the database. The company filter stays outside the query on purpose: it is a
    /// tenancy boundary and must not be something the caller can widen through $filter.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult Query([FromQuery] Guid companyId, ODataQueryOptions<ProductListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        // The options are applied by hand instead of through [EnableQuery] because this route is a
        // plain MVC route: without an EDM route the attribute cannot emit the { value, @odata.count }
        // envelope the grids expect, and $count would fail. The count is taken after $filter but
        // before $skip/$top so the pager knows the size of the whole result.
        var settings = new ODataQuerySettings();
        var query = productService.Query(companyId);

        if (options.Filter is not null)
            query = (IQueryable<ProductListItemDto>)options.Filter.ApplyTo(query, settings);

        var count = query.Count();

        if (options.OrderBy is not null)
            query = options.OrderBy.ApplyTo(query, settings);

        if (options.Skip is not null)
            query = options.Skip.ApplyTo(query, settings);

        query = options.Top is not null
            ? options.Top.ApplyTo(query, settings)
            : query.Take(MaxPageSize);

        return Ok(new ODataCollection<ProductListItemDto>(count, [.. query]));
    }

    /// <summary>Gets a single product.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductListItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Creates a product.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
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
    [Authorize(Policy = Policies.Write)]
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
