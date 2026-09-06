using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductListItemDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
}
