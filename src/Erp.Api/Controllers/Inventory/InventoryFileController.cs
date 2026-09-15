using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Inventory;

/// <summary>
/// The inventory communication file (Portaria 2/2015, as amended by Portaria 126/2019). The stock
/// belongs to Inventory and the company and product files to Core, so the host composes them.
/// </summary>
[ApiController]
[Route("api/inventory-file")]
[Authorize]
public sealed class InventoryFileController(
    IInventoryFileService inventoryFileService,
    ICompanyAdminService companyService,
    IProductService productService,
    ILogger<InventoryFileController> logger) : ControllerBase
{
    /// <summary>
    /// Builds the file for a period and returns it as an XML download. The stock reported is the
    /// stock held on the last day of the period. With <c>valued</c> it follows schema 2_01, which
    /// carries the stock value and is required for periods from 2021 onwards; without it, the
    /// older quantities-only schema.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [Produces("application/xml", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Build(
        [FromQuery] Guid companyId,
        [FromQuery] int fiscalYear,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken,
        [FromQuery] bool valued = true)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        if (fiscalYear < 2000 || fiscalYear > 2100)
            return BadRequest(new { error = "fiscalYear is out of range." });

        var company = await companyService.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return NotFound(new { error = "Company not found." });

        // The period normally ends on the last day of the year; a different date is allowed so a
        // stock position can be taken at any moment.
        var reference = endDate ?? new DateOnly(fiscalYear, 12, 31);

        var products = await productService.GetAllAsync(companyId, cancellationToken);

        var request = new InventoryFileRequest(
            companyId,
            fiscalYear,
            reference,
            new InventoryCompanyInfo(company.Name, company.TaxId, company.LegalName),
            [.. products.Select(product => new InventoryFileProductDto(
                product.ProductCode,
                product.Description,
                product.UnitOfMeasure,
                product.UnitCost,
                product.Barcode,
                product.InventoryCategory))],
            valued);

        try
        {
            var result = await inventoryFileService.BuildAsync(request, cancellationToken);

            // Stock valued at zero would be refused by the tax authority, so the count travels back.
            Response.Headers[Constants.Headers.InventoryProductsWithoutCost] = result.ProductsWithoutCost.ToString();
            Response.Headers[Constants.Headers.InventoryValidationErrors] = result.ValidationErrors.Count.ToString();

            foreach (var error in result.ValidationErrors)
                logger.LogWarning("Inventory file {FileName} does not conform to the schema: {Error}", result.FileName, error);

            return File(result.Content, "application/xml", result.FileName);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(InventoryFileController), nameof(Build));
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>What the file would contain, without generating it.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.Read)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid companyId,
        [FromQuery] int fiscalYear,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken,
        [FromQuery] bool valued = true)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var company = await companyService.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return NotFound(new { error = "Company not found." });

        var reference = endDate ?? new DateOnly(fiscalYear, 12, 31);
        var products = await productService.GetAllAsync(companyId, cancellationToken);

        var request = new InventoryFileRequest(
            companyId,
            fiscalYear,
            reference,
            new InventoryCompanyInfo(company.Name, company.TaxId, company.LegalName),
            [.. products.Select(product => new InventoryFileProductDto(
                product.ProductCode,
                product.Description,
                product.UnitOfMeasure,
                product.UnitCost,
                product.Barcode,
                product.InventoryCategory))],
            valued);

        try
        {
            var result = await inventoryFileService.BuildAsync(request, cancellationToken);

            return Ok(new
            {
                result.FileName,
                result.LineCount,
                result.TotalQuantity,
                result.TotalValue,
                result.ProductsWithoutCost,
                result.ProductsCostedFromFile,
                result.ProductsWithNegativeStock,
                ValidationErrors = result.ValidationErrors.Count,
                EndDate = reference
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(InventoryFileController), nameof(GetSummary));
            return BadRequest(new { error = ex.Message });
        }
    }
}
