using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class CustomerService(ICustomerStorage storage) : ICustomerService
{
    public async Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var customers = await storage.GetAllAsync(companyId, cancellationToken);
        return customers.Select(Map).ToList();
    }

    public IQueryable<PartnerDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await storage.GetByIdAsync(id, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        PartnerValidation.ValidateCreate(request);

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Customer code '{code}' already exists for this company.");

        var customer = new Customer
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            TaxId = request.TaxId.Trim(),
            Address = PartnerValidation.Normalize(request.Address),
            PostalCode = PartnerValidation.Normalize(request.PostalCode),
            City = PartnerValidation.Normalize(request.City),
            Country = request.Country,
            Email = PartnerValidation.Normalize(request.Email),
            Phone = PartnerValidation.Normalize(request.Phone)
        };

        await storage.AddAsync(customer, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        PartnerValidation.ValidateUpdate(request);

        var customer = await storage.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return null;

        customer.Name = request.Name.Trim();
        customer.TaxId = request.TaxId.Trim();
        customer.Address = PartnerValidation.Normalize(request.Address);
        customer.PostalCode = PartnerValidation.Normalize(request.PostalCode);
        customer.City = PartnerValidation.Normalize(request.City);
        customer.Country = request.Country;
        customer.Email = PartnerValidation.Normalize(request.Email);
        customer.Phone = PartnerValidation.Normalize(request.Phone);
        customer.IsActive = request.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    private static PartnerDto Map(Customer customer) =>
        new(customer.Id,
            customer.Code,
            customer.Name,
            customer.TaxId,
            customer.Address,
            customer.PostalCode,
            customer.City,
            customer.Country,
            customer.Email,
            customer.Phone,
            customer.IsActive);
}

public sealed class SupplierService(ISupplierStorage storage) : ISupplierService
{
    public async Task<IReadOnlyList<PartnerDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var suppliers = await storage.GetAllAsync(companyId, cancellationToken);
        return suppliers.Select(Map).ToList();
    }

    public IQueryable<PartnerDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<PartnerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await storage.GetByIdAsync(id, cancellationToken);
        return supplier is null ? null : Map(supplier);
    }

    public async Task<PartnerDto> CreateAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        PartnerValidation.ValidateCreate(request);

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Supplier code '{code}' already exists for this company.");

        var supplier = new Supplier
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            TaxId = request.TaxId.Trim(),
            Address = PartnerValidation.Normalize(request.Address),
            PostalCode = PartnerValidation.Normalize(request.PostalCode),
            City = PartnerValidation.Normalize(request.City),
            Country = request.Country,
            Email = PartnerValidation.Normalize(request.Email),
            Phone = PartnerValidation.Normalize(request.Phone)
        };

        await storage.AddAsync(supplier, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(supplier);
    }

    public async Task<PartnerDto?> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        PartnerValidation.ValidateUpdate(request);

        var supplier = await storage.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
            return null;

        supplier.Name = request.Name.Trim();
        supplier.TaxId = request.TaxId.Trim();
        supplier.Address = PartnerValidation.Normalize(request.Address);
        supplier.PostalCode = PartnerValidation.Normalize(request.PostalCode);
        supplier.City = PartnerValidation.Normalize(request.City);
        supplier.Country = request.Country;
        supplier.Email = PartnerValidation.Normalize(request.Email);
        supplier.Phone = PartnerValidation.Normalize(request.Phone);
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(supplier);
    }

    private static PartnerDto Map(Supplier supplier) =>
        new(supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country,
            supplier.Email,
            supplier.Phone,
            supplier.IsActive);
}

/// <summary>Customers and suppliers share the same shape, so they share the same rules.</summary>
internal static class PartnerValidation
{
    public static void ValidateCreate(CreatePartnerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxId);
    }

    public static void ValidateUpdate(UpdatePartnerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxId);
    }

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
