using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IVatRateService
{
    Task<IReadOnlyList<VatRateDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<VatRateDto> Query();

    Task<VatRateDto?> UpdateAsync(Guid id, UpdateVatRateRequest request, CancellationToken cancellationToken = default);
}
