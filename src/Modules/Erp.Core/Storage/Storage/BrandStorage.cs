using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Core.Storage.Storage;

public sealed class BrandStorage(AppDbContext dbContext) : IBrandStorage
{
    public async Task<IReadOnlyList<Brand>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Brand>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<BrandDto> Query(Guid companyId) =>
        dbContext.Set<Brand>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new BrandDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                IsActive = x.IsActive
            });

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<Brand>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Set<Brand>().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Brand brand, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Brand>().AddAsync(brand, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductFamilyStorage(AppDbContext dbContext) : IProductFamilyStorage
{
    public async Task<IReadOnlyList<ProductFamily>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<ProductFamily>()
            .AsNoTracking()
            .Include(x => x.Subfamilies)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<ProductFamilyDto> Query(Guid companyId) =>
        dbContext.Set<ProductFamily>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new ProductFamilyDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                IsActive = x.IsActive,
                SubfamilyCount = x.Subfamilies.Count
            });

    public Task<ProductFamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<ProductFamily>()
            .Include(x => x.Subfamilies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Set<ProductFamily>().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(ProductFamily family, CancellationToken cancellationToken = default) =>
        await dbContext.Set<ProductFamily>().AddAsync(family, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductSubfamilyStorage(AppDbContext dbContext) : IProductSubfamilyStorage
{
    public async Task<IReadOnlyList<ProductSubfamily>> GetAllAsync(
        Guid companyId,
        Guid? familyId = null,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<ProductSubfamily>()
            .AsNoTracking()
            .Include(x => x.Family)
            .Where(x => x.CompanyId == companyId && (familyId == null || x.FamilyId == familyId))
            .OrderBy(x => x.Family.Name)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<ProductSubfamilyDto> Query(Guid companyId) =>
        dbContext.Set<ProductSubfamily>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new ProductSubfamilyDto
            {
                Id = x.Id,
                FamilyId = x.FamilyId,
                FamilyName = x.Family.Name,
                Code = x.Code,
                Name = x.Name,
                IsActive = x.IsActive
            });

    public Task<ProductSubfamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<ProductSubfamily>()
            .Include(x => x.Family)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Set<ProductSubfamily>().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(ProductSubfamily subfamily, CancellationToken cancellationToken = default) =>
        await dbContext.Set<ProductSubfamily>().AddAsync(subfamily, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class ProductStorage(AppDbContext dbContext) : IProductStorage
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Product>()
            .AsNoTracking()
            .Include(x => x.Family)
            .Include(x => x.Subfamily)
            .Include(x => x.Brand)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.ProductCode)
            .ToListAsync(cancellationToken);

    public IQueryable<ProductListItemDto> Query(Guid companyId) =>
        dbContext.Set<Product>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new ProductListItemDto
            {
                Id = x.Id,
                ProductCode = x.ProductCode,
                Description = x.Description,
                ProductType = x.ProductType,
                UnitOfMeasure = x.UnitOfMeasure,
                UnitPrice = x.UnitPrice,
                DefaultTaxCode = x.DefaultTaxCode,
                DefaultTaxPercentage = x.DefaultTaxPercentage,
                Barcode = x.Barcode,
                FamilyId = x.FamilyId,
                FamilyName = x.Family!.Name,
                SubfamilyId = x.SubfamilyId,
                SubfamilyName = x.Subfamily!.Name,
                BrandId = x.BrandId,
                BrandName = x.Brand!.Name,
                IsActive = x.IsActive,
                UnitCost = x.UnitCost,
                InventoryCategory = x.InventoryCategory
            });

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<Product>()
            .Include(x => x.Family)
            .Include(x => x.Subfamily)
            .Include(x => x.Brand)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string productCode, CancellationToken cancellationToken = default) =>
        dbContext.Set<Product>().AnyAsync(x => x.CompanyId == companyId && x.ProductCode == productCode, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Product>().AddAsync(product, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class CustomerStorage(AppDbContext dbContext) : ICustomerStorage
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<PartnerDto> Query(Guid companyId) =>
        dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new PartnerDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                TaxId = x.TaxId,
                Address = x.Address,
                PostalCode = x.PostalCode,
                City = x.City,
                Country = x.Country,
                Email = x.Email,
                Phone = x.Phone,
                IsActive = x.IsActive
            });

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<Customer>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Set<Customer>().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Customer>().AddAsync(customer, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class SupplierStorage(AppDbContext dbContext) : ISupplierStorage
{
    public async Task<IReadOnlyList<Supplier>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<PartnerDto> Query(Guid companyId) =>
        dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new PartnerDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                TaxId = x.TaxId,
                Address = x.Address,
                PostalCode = x.PostalCode,
                City = x.City,
                Country = x.Country,
                Email = x.Email,
                Phone = x.Phone,
                IsActive = x.IsActive
            });

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<Supplier>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Set<Supplier>().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        await dbContext.Set<Supplier>().AddAsync(supplier, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
