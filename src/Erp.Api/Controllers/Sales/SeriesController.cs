using System.Security.Claims;
using Erp.Api.Authorization;
using Erp.Common;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Sales;

/// <summary>Document series and their communication to the tax authority.</summary>
[ApiController]
[Route("api/series")]
[Authorize]
[Produces("application/json")]
public sealed class SeriesController(ISeriesService seriesService) : ControllerBase
{
    /// <summary>Lists the series of a company.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<SeriesListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SeriesListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var result = await seriesService.GetAllAsync(companyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a single series.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesListItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await seriesService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Creates a series. It cannot issue documents until it is communicated.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeriesListItemDto>> Create(
        [FromBody] CreateSeriesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await seriesService.CreateAsync(request, GetCurrentUserId(), cancellationToken);
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
    /// Records the validation code returned by the tax authority for this series, which is what
    /// unlocks issuing and completes the ATCUD.
    /// </summary>
    [HttpPost("{id:guid}/communicate")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeriesListItemDto>> Communicate(
        Guid id,
        [FromBody] CommunicateSeriesRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ValidationCode))
            return BadRequest(new { error = "A validation code is required." });

        try
        {
            var result = await seriesService.CommunicateAsync(id, request.ValidationCode, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(Constants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
