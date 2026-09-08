using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The product list as a composable query, so the API can let OData push filtering, sorting
    /// and paging down to the database instead of returning the whole file.
    /// </summary>
    IQueryable<ProductListItemDto> Query(Guid companyId);

    Task<ProductListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductListItemDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductListItemDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
}
