using Erp.Sales.Domain;

namespace Erp.Sales.Infrastructure.Storage;

public interface IProductStorage
{
    Task<IReadOnlyList<Product>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(Guid companyId, string productCode, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);
}
