using Erp.Api.Services;
using Erp.Dependencies.PostalCodes;
using Erp.Dependencies.VatNumbers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// Free, external master-data lookups shared by the customer/supplier editors: address by
/// postal code (moradas.dev) and company data by NIF/NIPC (VIES). Neither call is company-scoped;
/// they only ever answer with public data.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class LookupsController(
    IPostalCodeLookupService postalCodeLookupService,
    IVatNumberValidationService vatNumberValidationService) : ControllerBase
{
    /// <summary>Address behind a Portuguese postal code ("0000-000"), or 404 when there is no match.</summary>
    [HttpGet("postal-codes/{postalCode}")]
    [ProducesResponseType<PostalCodeLookupResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostalCodeLookupResult>> GetPostalCode(
        string postalCode,
        CancellationToken cancellationToken)
    {
        var result = await postalCodeLookupService.LookupAsync(postalCode, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Module-11 structural check on <paramref name="nif"/>, followed by a VIES lookup only when
    /// the prefix identifies a company. Always answers 200: an invalid/individual NIF is a normal,
    /// informative result, not an error.
    /// </summary>
    [HttpGet("vat-numbers/{nif}")]
    [ProducesResponseType<VatNumberValidationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VatNumberValidationResult>> GetVatNumber(
        string nif,
        CancellationToken cancellationToken)
    {
        return Ok(await vatNumberValidationService.ValidateAsync(nif, cancellationToken));
    }
}
