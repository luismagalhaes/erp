using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

/// <summary>
/// An eco-fee is invoiced as its own pseudo-product (a SAF-T "I" — taxes and fees — article), so
/// creating one always creates the matching <see cref="Product"/> alongside it, and updating the
/// rate or the name keeps that product in step, the same way a document line always shows what an
/// eco-fee currently costs.
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

        var feeProduct = new Product
        {
            CompanyId = request.CompanyId,
            ProductCode = code,
            Description = description,
            ProductType = "I",
            InventoryCategory = "M",
            UnitOfMeasure = calculationBasis == EcoFeeCalculationBasis.PerKg ? "KG" : "UN",
            UnitPrice = request.Rate,
            DefaultTaxCode = FiscalPT.Documents.TaxCodes.Normal,
            DefaultTaxPercentage = 23m
        };

        await productStorage.AddAsync(feeProduct, cancellationToken);

        var ecoFeeType = new EcoFeeType
        {
            CompanyId = request.CompanyId,
            Code = code,
            Description = description,
            CalculationBasis = calculationBasis,
            Rate = request.Rate,
            ManagingEntityName = request.ManagingEntityName.Trim(),
            FeeProductId = feeProduct.Id,
            FeeProduct = feeProduct
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

        // The fee's own line always shows the current name and amount, since it is not an article a
        // user edits separately — it exists only because this eco-fee does.
        if (ecoFeeType.FeeProduct is { } feeProduct)
        {
            feeProduct.Description = ecoFeeType.Description;
            feeProduct.UnitPrice = ecoFeeType.Rate;
            feeProduct.IsActive = ecoFeeType.IsActive;
            feeProduct.UpdatedAtUtc = DateTime.UtcNow;
        }

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
            ecoFeeType.IsActive,
            ecoFeeType.FeeProduct?.ProductCode ?? string.Empty);
}
