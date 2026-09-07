namespace Erp.Core.Infrastructure.Contracts;

public sealed record WarehouseDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsDefault,
    bool IsActive);

public sealed record CreateWarehouseRequest(
    Guid CompanyId,
    string Code,
    string Name,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    bool IsDefault = false);

public sealed record UpdateWarehouseRequest(
    string Name,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsDefault,
    bool IsActive);
