using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IVatRateService
{
    Task<IReadOnlyList<VatRateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<VatRateDto?> UpdateAsync(Guid id, UpdateVatRateRequest request, CancellationToken cancellationToken = default);
}
