using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class CompanyAdminService(ICompanyStorage storage) : ICompanyAdminService
{
    public async Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await storage.GetAllAsync(cancellationToken);

        return list
            .Select(company => new CompanyListItemDto(
                company.Id,
                company.Name,
                company.LegalName,
                company.TaxId,
                company.IsActive))
            .ToList();
    }

    public async Task<CompanyDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await storage.GetByIdAsync(id, cancellationToken);
        return company is null ? null : Map(company);
    }

    public async Task<CompanyDetailDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxId);

        var taxId = request.TaxId.Trim();

        if (await storage.TaxIdExistsAsync(taxId, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"A company with tax id '{taxId}' already exists.");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            LegalName = request.LegalName?.Trim(),
            TaxId = taxId,
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await storage.AddAsync(company, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(company);
    }

    public async Task<CompanyDetailDto?> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxId);

        var company = await storage.GetByIdAsync(id, cancellationToken);
        if (company is null)
            return null;

        var taxId = request.TaxId.Trim();

        if (await storage.TaxIdExistsAsync(taxId, id, cancellationToken))
            throw new InvalidOperationException($"Another company already uses tax id '{taxId}'.");

        company.Name = request.Name.Trim();
        company.LegalName = request.LegalName?.Trim();
        company.TaxId = taxId;
        company.Email = request.Email?.Trim();
        company.Phone = request.Phone?.Trim();
        company.IsActive = request.IsActive;
        company.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(company);
    }

    private static CompanyDetailDto Map(Company company) =>
        new(company.Id,
            company.Name,
            company.LegalName,
            company.TaxId,
            company.Email,
            company.Phone,
            company.IsActive,
            company.CreatedAtUtc,
            company.UpdatedAtUtc);
}
