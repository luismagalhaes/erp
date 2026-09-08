using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Storage;

public interface IBrandStorage
{
    Task<IReadOnlyList<Brand>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing left open so the OData options of the grid are translated into SQL instead of
    /// being applied over rows already read.
    /// </summary>
    IQueryable<BrandDto> Query(Guid companyId);

    Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddAsync(Brand brand, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProductFamilyStorage
{
    Task<IReadOnlyList<ProductFamily>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<ProductFamilyDto> Query(Guid companyId);

    Task<ProductFamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddAsync(ProductFamily family, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProductSubfamilyStorage
{
    /// <param name="familyId">Limits the result to one family; null returns every subfamily.</param>
    Task<IReadOnlyList<ProductSubfamily>> GetAllAsync(Guid companyId, Guid? familyId = null, CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<ProductSubfamilyDto> Query(Guid companyId);
    Task<ProductSubfamily?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddAsync(ProductSubfamily subfamily, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProductStorage
{
    Task<IReadOnlyList<Product>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Products of a company already projected to the list DTO and left unmaterialised, so OData
    /// can translate the grid filtering, sorting and paging into a single SQL statement.
    /// </summary>
    IQueryable<ProductListItemDto> Query(Guid companyId);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string productCode, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICustomerStorage
{
    Task<IReadOnlyList<Customer>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<PartnerDto> Query(Guid companyId);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ISupplierStorage
{
    Task<IReadOnlyList<Supplier>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<PartnerDto> Query(Guid companyId);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
