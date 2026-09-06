using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class UserCompanyAdminService(IUserCompanyStorage storage) : IUserCompanyAdminService
{
    public async Task<IReadOnlyList<UserCompanyAdminDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await storage.GetAllAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    public async Task<UserCompanyAdminDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await storage.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<UserCompanyAdminDto> CreateAsync(CreateUserCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Role);
        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(request.CompanyId));

        var companyExists = await storage.CompanyExistsAsync(request.CompanyId, cancellationToken);
        if (!companyExists)
            throw new InvalidOperationException("Company not found.");

        var alreadyExists = await storage.ExistsAsync(request.UserId, request.CompanyId, cancellationToken);
        if (alreadyExists)
            throw new InvalidOperationException("UserCompany already exists.");

        var entity = new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId.Trim(),
            CompanyId = request.CompanyId,
            Role = request.Role.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        await storage.AddAsync(entity, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        var persisted = await storage.GetByIdAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("Unable to load created UserCompany.");

        return Map(persisted);
    }

    public async Task<UserCompanyAdminDto?> UpdateAsync(Guid id, UpdateUserCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Role);

        var entity = await storage.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        entity.Role = request.Role.Trim();
        entity.IsActive = request.IsActive;

        await storage.SaveChangesAsync(cancellationToken);

        var updated = await storage.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Unable to load updated UserCompany.");

        return Map(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await storage.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return false;

        storage.Remove(entity);
        await storage.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static UserCompanyAdminDto Map(UserCompany entity)
    {
        return new UserCompanyAdminDto(
            entity.Id,
            entity.UserId,
            entity.CompanyId,
            entity.Company.Name,
            entity.Role,
            entity.IsActive);
    }
}
