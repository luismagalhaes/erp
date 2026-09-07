using Erp.Api.Authorization;
using Erp.Core.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Sales;

/// <summary>
/// SAF-T (PT) export. This is the one place where the two modules meet: the company file belongs
/// to Core and the documents to Sales, so the host composes them.
/// </summary>
[ApiController]
[Route("api/saft")]
[Authorize]
public sealed class SaftController(
    ISaftExportService saftExportService,
    ICompanyAdminService companyService,
    ILogger<SaftController> logger) : ControllerBase
{
    /// <summary>Header carrying how many schema problems the generated file has, if any.</summary>
    public const string ValidationErrorsHeader = "X-Saft-Validation-Errors";

    /// <summary>What a period holds, so the user can check it before generating the file.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.Read)]
    [Produces("application/json")]
    [ProducesResponseType<SaftPeriodSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SaftPeriodSummaryDto>> GetSummary(
        [FromQuery] Guid companyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        try
        {
            return Ok(await saftExportService.GetSummaryAsync(companyId, startDate, endDate, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generates the SAF-T (PT) file for the period and returns it as an XML download.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [Produces("application/xml", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid companyId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var company = await companyService.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return NotFound(new { error = "Company not found." });

        var request = new SaftExportRequest(
            companyId,
            startDate,
            endDate,
            new SaftCompanyInfo(
                company.Name,
                company.TaxId,
                company.LegalName,
                company.Address,
                company.City,
                company.PostalCode,
                company.Country,
                company.Email,
                company.Phone));

        try
        {
            var result = await saftExportService.ExportAsync(request, cancellationToken);

            // The file is handed over either way, but a schema problem must not pass in silence.
            Response.Headers[ValidationErrorsHeader] = result.ValidationErrors.Count.ToString();

            foreach (var error in result.ValidationErrors)
                logger.LogWarning("SAF-T {FileName} does not conform to the schema: {Error}", result.FileName, error);

            return File(result.Content, "application/xml", result.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
