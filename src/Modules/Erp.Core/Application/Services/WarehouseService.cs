using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class WarehouseService(IWarehouseStorage storage) : IWarehouseService
{
    public async Task<IReadOnlyList<WarehouseDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var warehouses = await storage.GetAllAsync(companyId, cancellationToken);
        return warehouses.Select(Map).ToList();
    }

    public async Task<WarehouseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await storage.GetByIdAsync(id, cancellationToken);
        return warehouse is null ? null : Map(warehouse);
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"Warehouse code '{code}' already exists for this company.");

        var current = await storage.GetDefaultAsync(request.CompanyId, cancellationToken);

        var warehouse = new Warehouse
        {
            CompanyId = request.CompanyId,
            Code = code,
            Name = request.Name.Trim(),
            Address = Normalize(request.Address),
            City = Normalize(request.City),
            PostalCode = Normalize(request.PostalCode),
            Country = NormalizeCountry(request.Country),
            // The first warehouse of a company is the default, whatever the request says: a company
            // with stock and no default has nowhere to put it.
            IsDefault = request.IsDefault || current is null
        };

        if (warehouse.IsDefault && current is not null)
            current.IsDefault = false;

        await storage.AddAsync(warehouse, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(warehouse);
    }

    public async Task<WarehouseDto?> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var warehouse = await storage.GetByIdAsync(id, cancellationToken);
        if (warehouse is null)
            return null;

        if (request.IsDefault && !warehouse.IsDefault)
        {
            // Only one default per company, so the previous one steps down.
            var current = await storage.GetDefaultAsync(warehouse.CompanyId, cancellationToken);

            if (current is not null && current.Id != warehouse.Id)
                current.IsDefault = false;
        }

        if (warehouse.IsDefault && !request.IsDefault)
        {
            throw new InvalidOperationException(
                "A company must keep a default warehouse. Mark another one as default instead of clearing this one.");
        }

        if (warehouse.IsDefault && !request.IsActive)
            throw new InvalidOperationException("The default warehouse cannot be deactivated.");

        warehouse.Name = request.Name.Trim();
        warehouse.Address = Normalize(request.Address);
        warehouse.City = Normalize(request.City);
        warehouse.PostalCode = Normalize(request.PostalCode);
        warehouse.Country = NormalizeCountry(request.Country);
        warehouse.IsDefault = request.IsDefault;
        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(warehouse);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeCountry(string? country) =>
        string.IsNullOrWhiteSpace(country) ? "PT" : country.Trim().ToUpperInvariant();

    private static WarehouseDto Map(Warehouse warehouse) =>
        new(warehouse.Id,
            warehouse.CompanyId,
            warehouse.Code,
            warehouse.Name,
            warehouse.Address,
            warehouse.City,
            warehouse.PostalCode,
            warehouse.Country,
            warehouse.IsDefault,
            warehouse.IsActive);
}
