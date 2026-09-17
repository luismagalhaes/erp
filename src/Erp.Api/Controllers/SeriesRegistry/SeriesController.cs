using System.Security.Claims;
using Erp.Api.Services;
using Erp.Common;
using Erp.FiscalPT.AtWebservice.Series;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.SeriesRegistry;

/// <summary>Document series and their communication to the tax authority.</summary>
[ApiController]
[Route("api/series")]
[Authorize]
[Produces("application/json")]
public sealed class SeriesController(ISeriesService seriesService, ILogger<SeriesController> logger) : ControllerBase
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

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<SeriesListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<SeriesListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<SeriesListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(seriesService.Query(companyId), options));
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
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(SeriesController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SeriesController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Changes what documents of this series do to stock, which is the only thing about a series
    /// that may change once it exists: everything else is communicated to the tax authority or
    /// already written into the documents issued from it.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesListItemDto>> Update(
        Guid id,
        [FromBody] UpdateSeriesRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        try
        {
            var result = await seriesService.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(SeriesController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Registers the series with the AT webservice and records the validation code it returns,
    /// which is what unlocks issuing and completes the ATCUD.
    /// </summary>
    [HttpPost("{id:guid}/communicate")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SeriesListItemDto>> Communicate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await seriesService.CommunicateAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AtSeriesException ex)
        {
            logger.LogWarning(
                ex, "{Controller}.{Method}: AT rejected the series (code {ReturnCode}).",
                nameof(SeriesController), nameof(Communicate), ex.ReturnCode);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, atReturnCode = ex.ReturnCode });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SeriesController), nameof(Communicate));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Records a validation code obtained outside this system — e.g. straight from the Portal das
    /// Finanças — for when the AT webservice itself is not reachable or not yet configured.
    /// </summary>
    [HttpPost("{id:guid}/communicate-manually")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeriesListItemDto>> CommunicateManually(
        Guid id, [FromBody] CommunicateSeriesManuallyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ValidationCode))
            return BadRequest(new { error = "A validation code is required." });

        try
        {
            var result = await seriesService.CommunicateManuallyAsync(id, request.ValidationCode, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SeriesController), nameof(CommunicateManually));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a series communicated by mistake, before any document was issued on it. Not
    /// reversible.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SeriesListItemDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await seriesService.CancelAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AtSeriesException ex)
        {
            logger.LogWarning(
                ex, "{Controller}.{Method}: AT rejected the cancellation (code {ReturnCode}).",
                nameof(SeriesController), nameof(Cancel), ex.ReturnCode);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, atReturnCode = ex.ReturnCode });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SeriesController), nameof(Cancel));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Tells the AT the series is done: valid for the documents already issued, but not to be used
    /// again from here on.
    /// </summary>
    [HttpPost("{id:guid}/finalize")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SeriesListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SeriesListItemDto>> Finalize(
        Guid id, [FromBody] FinalizeSeriesRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await seriesService.FinalizeAsync(id, request?.Justificacao, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AtSeriesException ex)
        {
            logger.LogWarning(
                ex, "{Controller}.{Method}: AT rejected the finalization (code {ReturnCode}).",
                nameof(SeriesController), nameof(Finalize), ex.ReturnCode);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, atReturnCode = ex.ReturnCode });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SeriesController), nameof(Finalize));
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(Constants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
