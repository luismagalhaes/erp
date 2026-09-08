using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.FiscalPT;
using Erp.FiscalPT.Saft;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Erp.Api.Controllers.FiscalPT;

/// <summary>
/// SAF-T (PT) export. The host composes it: the entity comes from Core, the documents from whichever
/// modules registered a source, and the file itself is built by <see cref="SaftExporter"/>.
/// </summary>
[ApiController]
[Route("api/saft")]
[Authorize]
public sealed class SaftController(
    SaftExporter exporter,
    ISaftSummaryService summaryService,
    ICompanyAdminService companyService,
    ISupplierService supplierService,
    IOptions<FiscalOptions> fiscalOptions,
    ILogger<SaftController> logger) : ControllerBase
{
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
            return Ok(await summaryService.GetSummaryAsync(companyId, startDate, endDate, cancellationToken));
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

        var spec = new SaftExportSpec(
            companyId,
            startDate,
            endDate,
            // The billing file. Self-billing ("S") is a file of its own, per supplier.
            SaftFileType.Billing,
            ToEntity(company),
            fiscalOptions.Value.ToProducerInfo());

        return await ExportAsync(spec, cancellationToken);
    }

    /// <summary>
    /// Generates the self-billing SAF-T (PT) file for one supplier and returns it as an XML
    /// download.
    /// </summary>
    /// <remarks>
    /// A separate file, of type <c>"S"</c>, and one per supplier: the documents in it are their
    /// sales, so their tax id is in the header. We appear in it as the customer, which is what the
    /// <c>SelfBillingIndicator</c> says. None of these documents belong in the billing file above.
    /// </remarks>
    [HttpGet("self-billing")]
    [Authorize(Policy = Policies.Read)]
    [Produces("application/xml", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportSelfBilling(
        [FromQuery] Guid companyId,
        [FromQuery] Guid supplierId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        if (supplierId == Guid.Empty)
            return BadRequest(new { error = "supplierId is required: a self-billing file is one per supplier." });

        var company = await companyService.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return NotFound(new { error = "Company not found." });

        var supplier = await supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        // Their tax id is the header of this file. Without one there is no file to produce.
        if (string.IsNullOrWhiteSpace(supplier.TaxId))
            return BadRequest(new { error = "The supplier has no tax id, which the SAF-T header requires." });

        var spec = new SaftExportSpec(
            companyId,
            startDate,
            endDate,
            SaftFileType.SelfBilling,
            // The subject is the supplier: the file reports their sales, not ours.
            new SaftEntityInfo(
                supplier.Name,
                supplier.TaxId,
                supplier.Name,
                supplier.Address,
                supplier.City,
                supplier.PostalCode,
                supplier.Country,
                supplier.Email,
                supplier.Phone),
            fiscalOptions.Value.ToProducerInfo(),
            // And we are the customer in those sales, because we bought the goods.
            ToEntity(company));

        return await ExportAsync(spec, cancellationToken);
    }

    private async Task<IActionResult> ExportAsync(SaftExportSpec spec, CancellationToken cancellationToken)
    {
        try
        {
            var result = await exporter.ExportAsync(spec, cancellationToken);

            // The file is handed over either way, but a schema problem must not pass in silence.
            Response.Headers[Constants.Headers.SaftValidationErrors] = result.ValidationErrors.Count.ToString();

            foreach (var error in result.ValidationErrors)
                logger.LogWarning("SAF-T {FileName} does not conform to the schema: {Error}", result.FileName, error);

            return File(result.Content, "application/xml", result.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static SaftEntityInfo ToEntity(CompanyDetailDto company) =>
        new(company.Name,
            company.TaxId,
            company.LegalName,
            company.Address,
            company.City,
            company.PostalCode,
            company.Country,
            company.Email,
            company.Phone);
}
