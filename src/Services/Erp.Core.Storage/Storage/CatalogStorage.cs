using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class BrandStorage(CoreDbContext dbContext) : IBrandStorage
{
    public async Task<IReadOnlyList<Brand>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Brands
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Brands.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Brands.AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Brand brand, CancellationToken cancellationToken = default) =>
        await dbContext.Brands.AddAsync(brand, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductFamilyStorage(CoreDbContext dbContext) : IProductFamilyStorage
{
    public async Task<IReadOnlyList<ProductFamily>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.ProductFamilies
            .AsNoTracking()
            .Include(x => x.Subfamilies)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<ProductFamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProductFamilies
            .Include(x => x.Subfamilies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.ProductFamilies.AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(ProductFamily family, CancellationToken cancellationToken = default) =>
        await dbContext.ProductFamilies.AddAsync(family, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductSubfamilyStorage(CoreDbContext dbContext) : IProductSubfamilyStorage
{
    public async Task<IReadOnlyList<ProductSubfamily>> GetAllAsync(
        Guid companyId,
        Guid? familyId = null,
        CancellationToken cancellationToken = default) =>
        await dbContext.ProductSubfamilies
            .AsNoTracking()
            .Include(x => x.Family)
            .Where(x => x.CompanyId == companyId && (familyId == null || x.FamilyId == familyId))
            .OrderBy(x => x.Family.Name)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<ProductSubfamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProductSubfamilies
            .Include(x => x.Family)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.ProductSubfamilies.AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(ProductSubfamily subfamily, CancellationToken cancellationToken = default) =>
        await dbContext.ProductSubfamilies.AddAsync(subfamily, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductStorage(CoreDbContext dbContext) : IProductStorage
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Family)
            .Include(x => x.Subfamily)
            .Include(x => x.Brand)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.ProductCode)
            .ToListAsync(cancellationToken);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.Family)
            .Include(x => x.Subfamily)
            .Include(x => x.Brand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string productCode, CancellationToken cancellationToken = default) =>
        dbContext.Products.AnyAsync(x => x.CompanyId == companyId && x.ProductCode == productCode, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await dbContext.Products.AddAsync(product, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class CustomerStorage(CoreDbContext dbContext) : ICustomerStorage
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await dbContext.Customers.AddAsync(customer, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class SupplierStorage(CoreDbContext dbContext) : ISupplierStorage
{
    public async Task<IReadOnlyList<Supplier>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        await dbContext.Suppliers.AddAsync(supplier, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
