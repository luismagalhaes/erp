using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Microsoft.Extensions.Logging;

namespace Erp.Api.Services;

/// <summary>The demo data seeded for a company: how many rows of each kind, or that it was already there.</summary>
public sealed record DemoDataResult(
    bool Applied,
    int Families,
    int Subfamilies,
    int Brands,
    int Products,
    int SeriesCommunicated);

/// <summary>
/// Seeds a company with realistic auto-parts master data — families, subfamilies, brands and
/// products — plus the standard document series, communicated with a fake validation code so the
/// company can issue documents right away.
/// </summary>
/// <remarks>
/// Composed here, in the host, for the same reason a new company's series are (see
/// <c>CompaniesController.SeedSeriesAsync</c>): it draws from both the catalogue in Erp.Core and
/// the series registry, and neither module knows about the other. This exists purely to make a
/// demonstration or a training environment look like a real, working company in a few seconds —
/// it is never called automatically, only from the explicit "Aplicar Demo" action.
/// </remarks>
public sealed class DemoDataService(
    IProductFamilyService familyService,
    IProductSubfamilyService subfamilyService,
    IBrandService brandService,
    IProductService productService,
    ISeriesService seriesService,
    ILogger<DemoDataService> logger)
{
    private const string CodePrefix = "DEMO-";
    private const string FakeValidationCode = "XXXX";

    /// <summary>
    /// One family, and the base part name for each of its two subfamilies — car-parts categories a
    /// workshop or a parts shop would actually stock.
    /// </summary>
    private static readonly (string FamilyName, (string SubfamilyName, string PartName, decimal BasePrice)[] Subfamilies)[] Catalogue =
    [
        ("Travões", [("Pastilhas de Travão", "Jogo de Pastilhas de Travão", 32m), ("Discos de Travão", "Disco de Travão Ventilado", 45m)]),
        ("Motor", [("Correias e Correntes", "Correia de Distribuição", 28m), ("Velas e Bobinas", "Vela de Ignição", 8m)]),
        ("Suspensão", [("Amortecedores", "Amortecedor Dianteiro", 55m), ("Rótulas e Braços", "Rótula de Direção", 18m)]),
        ("Elétrica", [("Baterias", "Bateria 12V 60Ah", 95m), ("Alternadores e Motores de Arranque", "Alternador", 140m)]),
        ("Filtros", [("Filtros de Óleo", "Filtro de Óleo", 7m), ("Filtros de Ar", "Filtro de Ar", 9m)]),
    ];

    /// <summary>Auto-parts brands well known enough to make a demo catalogue feel real.</summary>
    private static readonly string[] Brands =
    [
        "Bosch", "Brembo", "Valeo", "Sachs", "Mann-Filter", "Febi Bilstein", "TRW", "NGK", "Denso",
        "Delphi", "Continental", "ZF", "Monroe", "Bilstein", "Gates", "Dayco", "Mahle", "Hella",
        "Varta", "Bosal", "Textar", "ATE", "Lemförder", "Meyle", "SKF", "FAG", "Ruville", "Champion",
        "Pierburg", "KYB"
    ];

    private const int ProductCount = 100;

    /// <summary>
    /// Creates the demo master data and communicates the company's standard series, unless demo
    /// data was already applied to this company — in which case nothing is created again.
    /// </summary>
    public async Task<DemoDataResult> ApplyAsync(Guid companyId, string? userId, CancellationToken cancellationToken = default)
    {
        var firstFamilyCode = $"{CodePrefix}F01";

        if (await familyService.CodeExistsAsync(companyId, firstFamilyCode, cancellationToken))
        {
            logger.LogInformation("Demo data was already applied to company {CompanyId}; nothing to do.", companyId);
            return new DemoDataResult(false, 0, 0, 0, 0, 0);
        }

        var families = await CreateFamiliesAsync(companyId, cancellationToken);
        var subfamilies = await CreateSubfamiliesAsync(companyId, families, cancellationToken);
        var brands = await CreateBrandsAsync(companyId, cancellationToken);
        var products = await CreateProductsAsync(companyId, families, subfamilies, brands, cancellationToken);
        var seriesCommunicated = await CommunicateStandardSeriesAsync(companyId, userId, cancellationToken);

        logger.LogInformation(
            "Applied demo data to company {CompanyId}: {Families} families, {Subfamilies} subfamilies, {Brands} brands, {Products} products, {Series} series communicated.",
            companyId, families.Count, subfamilies.Count, brands.Count, products, seriesCommunicated);

        return new DemoDataResult(true, families.Count, subfamilies.Count, brands.Count, products, seriesCommunicated);
    }

    private async Task<List<ProductFamilyDto>> CreateFamiliesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var families = new List<ProductFamilyDto>();

        for (var i = 0; i < Catalogue.Length; i++)
        {
            var code = $"{CodePrefix}F{i + 1:00}";
            var family = await familyService.CreateAsync(
                new CreateProductFamilyRequest(companyId, code, Catalogue[i].FamilyName), cancellationToken);
            families.Add(family);
        }

        return families;
    }

    private async Task<List<ProductSubfamilyDto>> CreateSubfamiliesAsync(
        Guid companyId, List<ProductFamilyDto> families, CancellationToken cancellationToken)
    {
        var subfamilies = new List<ProductSubfamilyDto>();
        var counter = 1;

        for (var familyIndex = 0; familyIndex < Catalogue.Length; familyIndex++)
        {
            foreach (var (subfamilyName, _, _) in Catalogue[familyIndex].Subfamilies)
            {
                var code = $"{CodePrefix}S{counter++:00}";
                var subfamily = await subfamilyService.CreateAsync(
                    new CreateProductSubfamilyRequest(companyId, families[familyIndex].Id, code, subfamilyName),
                    cancellationToken);
                subfamilies.Add(subfamily);
            }
        }

        return subfamilies;
    }

    private async Task<List<BrandDto>> CreateBrandsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var brands = new List<BrandDto>();

        for (var i = 0; i < Brands.Length; i++)
        {
            var code = $"{CodePrefix}B{i + 1:00}";
            var brand = await brandService.CreateAsync(new CreateBrandRequest(companyId, code, Brands[i]), cancellationToken);
            brands.Add(brand);
        }

        return brands;
    }

    private async Task<int> CreateProductsAsync(
        Guid companyId,
        List<ProductFamilyDto> families,
        List<ProductSubfamilyDto> subfamilies,
        List<BrandDto> brands,
        CancellationToken cancellationToken)
    {
        // Flattened so product #n picks its family/subfamily/part name by simple modulo, cycling
        // through every subfamily before repeating one.
        var slots = new List<(ProductFamilyDto Family, ProductSubfamilyDto Subfamily, string PartName, decimal BasePrice)>();

        var subfamilyIndex = 0;
        for (var familyIndex = 0; familyIndex < Catalogue.Length; familyIndex++)
        {
            foreach (var (_, partName, basePrice) in Catalogue[familyIndex].Subfamilies)
            {
                slots.Add((families[familyIndex], subfamilies[subfamilyIndex], partName, basePrice));
                subfamilyIndex++;
            }
        }

        for (var i = 1; i <= ProductCount; i++)
        {
            var slot = slots[(i - 1) % slots.Count];
            var brand = brands[(i - 1) % brands.Count];

            var productCode = $"{CodePrefix}P{i:000}";
            var description = $"{slot.PartName} {brand.Name} Ref.{i:0000}";

            // A little variation so a hundred products are not a hundred identical prices.
            var unitPrice = Math.Round(slot.BasePrice * (1 + (i % 7) * 0.05m), 2);
            var unitCost = Math.Round(unitPrice * 0.65m, 2);

            await productService.CreateAsync(
                new CreateProductRequest(
                    CompanyId: companyId,
                    ProductCode: productCode,
                    Description: description,
                    UnitPrice: unitPrice,
                    UnitCost: unitCost,
                    FamilyId: slot.Family.Id,
                    SubfamilyId: slot.Subfamily.Id,
                    BrandId: brand.Id),
                cancellationToken);
        }

        return ProductCount;
    }

    /// <summary>
    /// Communicates every one of the company's standard series that cannot issue yet, with a fake
    /// validation code, so they behave as if the tax authority had already approved them — without
    /// ever calling the real AT webservice.
    /// </summary>
    /// <remarks>
    /// By the time "Aplicar Demo" runs, the company's standard series already exist —
    /// <c>CompaniesController.Create</c> seeds them the moment the company itself is created, via
    /// this same <see cref="ISeriesService.CreateStandardSetAsync"/>. That call only ever returns
    /// the series it just created, skipping whatever already exists (see its own doc comment), so
    /// calling it again here returned an empty list every time and nothing was ever communicated.
    /// <see cref="ISeriesService.GetAllAsync"/> is what actually has to be read: every series the
    /// company already has, not just newly created ones. The <c>CreateStandardSetAsync</c> call
    /// stays — harmless when everything already exists, and still needed for a company demo data
    /// is applied to before it has any series at all.
    /// </remarks>
    private async Task<int> CommunicateStandardSeriesAsync(Guid companyId, string? userId, CancellationToken cancellationToken)
    {
        await seriesService.CreateStandardSetAsync(companyId, DateTime.Today.Year, userId, cancellationToken);

        var series = await seriesService.GetAllAsync(companyId, cancellationToken);

        var communicated = 0;

        foreach (var item in series)
        {
            if (item.CanIssue)
                continue;

            await seriesService.CommunicateManuallyAsync(item.Id, FakeValidationCode, cancellationToken);
            communicated++;
        }

        return communicated;
    }
}
