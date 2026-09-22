using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

/// <summary>
/// An eco-fee is invoiced as its own document line (SAF-T ProductType "I" — taxes and fees), built
/// straight from this type's own <see cref="EcoFeeType.Code"/>, <see cref="EcoFeeType.Description"/>
/// and <see cref="EcoFeeType.Rate"/> at the moment it rides along an article — it needs no catalog
/// article of its own.
/// </summary>
public sealed class EcoFeeTypeService(IEcoFeeTypeStorage storage, IProductStorage productStorage) : IEcoFeeTypeService
{
    private static readonly string[] CalculationBases = [nameof(EcoFeeCalculationBasis.PerUnit), nameof(EcoFeeCalculationBasis.PerKg)];

    public async Task<IReadOnlyList<EcoFeeTypeDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ecoFeeTypes = await storage.GetAllAsync(companyId, cancellationToken);
        return ecoFeeTypes.Select(Map).ToList();
    }

    public IQueryable<EcoFeeTypeDto> Query(Guid companyId) => storage.Query(companyId);

    public async Task<EcoFeeTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ecoFeeType = await storage.GetByIdAsync(id, cancellationToken);
        return ecoFeeType is null ? null : Map(ecoFeeType);
    }

    public async Task<EcoFeeTypeDto> CreateAsync(CreateEcoFeeTypeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagingEntityName);

        if (!CalculationBases.Contains(request.CalculationBasis, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown calculation basis '{request.CalculationBasis}'.", nameof(request));

        if (request.Rate <= 0)
            throw new ArgumentException("The rate must be positive.", nameof(request));

        var code = request.Code.Trim();

        if (await storage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Eco-fee code '{code}' already exists for this company.");

        // The fee's own line uses the same code, so it also has to be free of the article catalogue.
        if (await productStorage.CodeExistsAsync(request.CompanyId, code, cancellationToken))
            throw new InvalidOperationException($"Product code '{code}' is already used by an article, so it cannot name the fee's own line.");

        var description = request.Description.Trim();
        var calculationBasis = Enum.Parse<EcoFeeCalculationBasis>(request.CalculationBasis);

        var ecoFeeType = new EcoFeeType
        {
            CompanyId = request.CompanyId,
            Code = code,
            Description = description,
            CalculationBasis = calculationBasis,
            Rate = request.Rate,
            ManagingEntityName = request.ManagingEntityName.Trim()
        };

        await storage.AddAsync(ecoFeeType, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(ecoFeeType);
    }

    public async Task<EcoFeeTypeDto?> UpdateAsync(Guid id, UpdateEcoFeeTypeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ManagingEntityName);

        if (request.Rate <= 0)
            throw new ArgumentException("The rate must be positive.", nameof(request));

        var ecoFeeType = await storage.GetByIdAsync(id, cancellationToken);
        if (ecoFeeType is null)
            return null;

        ecoFeeType.Description = request.Description.Trim();
        ecoFeeType.Rate = request.Rate;
        ecoFeeType.ManagingEntityName = request.ManagingEntityName.Trim();
        ecoFeeType.IsActive = request.IsActive;
        ecoFeeType.UpdatedAtUtc = DateTime.UtcNow;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(ecoFeeType);
    }

    private static EcoFeeTypeDto Map(EcoFeeType ecoFeeType) =>
        new(ecoFeeType.Id,
            ecoFeeType.Code,
            ecoFeeType.Description,
            ecoFeeType.CalculationBasis.ToString(),
            ecoFeeType.Rate,
            ecoFeeType.ManagingEntityName,
            ecoFeeType.IsActive);
}
