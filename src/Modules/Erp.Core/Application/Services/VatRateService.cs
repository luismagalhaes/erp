using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class VatRateService(IVatRateStorage storage) : IVatRateService
{
    public async Task<IReadOnlyList<VatRateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rates = await storage.GetAllAsync(cancellationToken);
        return rates.Select(Map).ToList();
    }

    public IQueryable<VatRateDto> Query() => storage.Query();

    public async Task<VatRateDto?> UpdateAsync(Guid id, UpdateVatRateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rate = await storage.GetByIdAsync(id, cancellationToken);
        if (rate is null)
            return null;

        rate.Percentage = request.Percentage;
        rate.IsActive = request.IsActive;
        rate.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(rate);
    }

    private static VatRateDto Map(VatRate rate) =>
        new(rate.Id, rate.FiscalRegion, rate.Code, rate.Label, rate.Percentage, rate.IsActive);
}
