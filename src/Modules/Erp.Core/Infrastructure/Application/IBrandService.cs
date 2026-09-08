using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<BrandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<BrandDto?> UpdateAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
}
