using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// VAT rates by fiscal region. Not company scoped — set by Portuguese law, the same for every
/// tenant — so there is no companyId here and no create/delete: the rows are seeded once by
/// migration, only the percentage and active flag can change, and only a SuperAdmin can change them.
/// </summary>
[ApiController]
[Route("api/vat-rates")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class VatRatesController(IVatRateService vatRateService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VatRateDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VatRateDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await vatRateService.GetAllAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<VatRateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VatRateDto>> Update(
        Guid id,
        [FromBody] UpdateVatRateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await vatRateService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }
}
