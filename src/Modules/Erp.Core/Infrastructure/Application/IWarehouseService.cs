using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<WarehouseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default);

    Task<WarehouseDto?> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default);
}
