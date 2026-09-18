using Erp.Common;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class CompanyAdminService(
    ICompanyStorage storage,
    IWarehouseStorage warehouseStorage,
    IProductStorage productStorage,
    IEcoFeeTypeStorage ecoFeeTypeStorage) : ICompanyAdminService
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

    public IQueryable<CompanyListItemDto> Query() => storage.Query();

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

        if (!PostalCodes.IsValid(request.PostalCode, request.Country))
            throw new ArgumentException($"Postal code '{request.PostalCode}' is not in the Portuguese format XXXX-XXX.", nameof(request));

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
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = NormalizeCountry(request.Country),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await storage.AddAsync(company, cancellationToken);

        // Saved together with the company: stock management has nowhere to put anything until a
        // company has a default warehouse, so one never exists without the other.
        await warehouseStorage.AddAsync(new Warehouse
        {
            CompanyId = company.Id,
            Code = Constants.DefaultWarehouse.Code,
            Name = Constants.DefaultWarehouse.Name,
            Address = company.Address,
            City = company.City,
            PostalCode = company.PostalCode,
            Country = company.Country,
            IsDefault = true
        }, cancellationToken);

        // Saved together with the company too: batteries and lubricating oils are the eco-fees a
        // small business runs into first, so they exist from day one instead of after a support call.
        await CreateDefaultEcoFeeTypesAsync(company.Id, cancellationToken);

        await storage.SaveChangesAsync(cancellationToken);

        return Map(company);
    }

    private async Task CreateDefaultEcoFeeTypesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        foreach (var seed in Constants.DefaultEcoFeeTypes.All)
        {
            var feeProduct = new Product
            {
                CompanyId = companyId,
                ProductCode = seed.Code,
                Description = seed.Description,
                ProductType = "I",
                InventoryCategory = "M",
                UnitOfMeasure = seed.CalculationBasis == nameof(EcoFeeCalculationBasis.PerKg) ? "KG" : "UN",
                UnitPrice = seed.Rate,
                DefaultTaxCode = FiscalPT.Documents.TaxCodes.Normal,
                DefaultTaxPercentage = 23m
            };

            await productStorage.AddAsync(feeProduct, cancellationToken);

            await ecoFeeTypeStorage.AddAsync(new EcoFeeType
            {
                CompanyId = companyId,
                Code = seed.Code,
                Description = seed.Description,
                CalculationBasis = Enum.Parse<EcoFeeCalculationBasis>(seed.CalculationBasis),
                Rate = seed.Rate,
                ManagingEntityName = seed.ManagingEntityName,
                FeeProductId = feeProduct.Id,
                FeeProduct = feeProduct
            }, cancellationToken);
        }
    }

    public async Task<CompanyDetailDto?> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaxId);

        if (!PostalCodes.IsValid(request.PostalCode, request.Country))
            throw new ArgumentException($"Postal code '{request.PostalCode}' is not in the Portuguese format XXXX-XXX.", nameof(request));

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
        company.Address = request.Address?.Trim();
        company.City = request.City?.Trim();
        company.PostalCode = request.PostalCode?.Trim();
        company.Country = NormalizeCountry(request.Country);
        company.IsActive = request.IsActive;
        company.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(company);
    }

    /// <summary>The SAF-T header carries a two letter country code, so an empty one falls back to PT.</summary>
    private static string NormalizeCountry(string? country) =>
        string.IsNullOrWhiteSpace(country) ? "PT" : country.Trim().ToUpperInvariant();

    private static CompanyDetailDto Map(Company company) =>
        new(company.Id,
            company.Name,
            company.LegalName,
            company.TaxId,
            company.Email,
            company.Phone,
            company.Address,
            company.City,
            company.PostalCode,
            company.Country,
            company.IsActive,
            company.CreatedAtUtc,
            company.UpdatedAtUtc);
}
