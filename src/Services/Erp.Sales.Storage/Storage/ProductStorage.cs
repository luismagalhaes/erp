using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Storage;

public sealed class ProductStorage(SalesDbContext dbContext) : IProductStorage
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.ProductCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> CodeExistsAsync(Guid companyId, string productCode, CancellationToken cancellationToken = default)
    {
        return dbContext.Products
            .AnyAsync(x => x.CompanyId == companyId && x.ProductCode == productCode, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }
}
