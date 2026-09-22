using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Erp.Api.Tests;

/// <summary>
/// Communicating the demo company's series is the part <c>DemoDataService</c> got wrong: it asked
/// <c>ISeriesService.CreateStandardSetAsync</c> for the series to communicate, but that call only
/// ever returns series it just created — by the time "Aplicar Demo" runs, the company already has
/// them (<c>CompaniesController.Create</c> seeds them at company creation), so it always came back
/// empty and nothing was ever communicated.
/// </summary>
public class DemoDataServiceTests
{
    private readonly IProductFamilyService _families = Substitute.For<IProductFamilyService>();
    private readonly IProductSubfamilyService _subfamilies = Substitute.For<IProductSubfamilyService>();
    private readonly IBrandService _brands = Substitute.For<IBrandService>();
    private readonly IProductService _products = Substitute.For<IProductService>();
    private readonly IEcoFeeTypeService _ecoFeeTypes = Substitute.For<IEcoFeeTypeService>();
    private readonly ISeriesService _series = Substitute.For<ISeriesService>();
    private readonly IDocumentNumbers _documentNumbers = Substitute.For<IDocumentNumbers>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _batteryEcoFeeTypeId = Guid.NewGuid();

    public DemoDataServiceTests()
    {
        // MasterDataCodes opens a transaction per code it hands out — an unconfigured
        // BeginTransactionAsync returns null, and CommitAsync is then called on that null.
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<ITransaction>());

        _families.CodeExistsAsync(_companyId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Every company already has its two default Ecovalor types by the time demo data runs —
        // seeded when the company itself was created, same as the series.
        _ecoFeeTypes.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(
        [
            new EcoFeeTypeDto(_batteryEcoFeeTypeId, Constants.DefaultEcoFeeTypes.All[0].Code, "Ecovalor - Pilhas e baterias", "PerUnit", 0.03m, "Ecopilhas / Amb3E", true),
            new EcoFeeTypeDto(Guid.NewGuid(), Constants.DefaultEcoFeeTypes.All[1].Code, "Ecovalor - Óleos lubrificantes", "PerKg", 0.10m, "SOGILUB", true)
        ]);

        _families.CreateAsync(Arg.Any<CreateProductFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProductFamilyDto(Guid.NewGuid(), call.Arg<CreateProductFamilyRequest>().Code, "Família", true, 0));

        _subfamilies.CreateAsync(Arg.Any<CreateProductSubfamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreateProductSubfamilyRequest>();
                return new ProductSubfamilyDto(Guid.NewGuid(), request.FamilyId, "Família", request.Code, request.Name, true);
            });

        _brands.CreateAsync(Arg.Any<CreateBrandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new BrandDto(Guid.NewGuid(), call.Arg<CreateBrandRequest>().Code, call.Arg<CreateBrandRequest>().Name, true));

        _products.CreateAsync(Arg.Any<CreateProductRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProductListItemDto(
                Guid.NewGuid(), call.Arg<CreateProductRequest>().ProductCode, "Produto", "I", "UN", 10m, "NOR", 23m,
                barcode: null, familyId: null, familyName: null, subfamilyId: null, subfamilyName: null,
                brandId: null, brandName: null, isActive: true));
    }

    private DemoDataService CreateService() =>
        new(_families, _subfamilies, _brands, _products, _ecoFeeTypes, _series,
            new MasterDataCodes(_documentNumbers, _unitOfWork), NullLogger<DemoDataService>.Instance);

    /// <summary>The actual bug: series that already existed before "Aplicar Demo" ran must still get communicated.</summary>
    [Fact]
    public async Task ApplyAsync_communicates_series_that_already_existed_before_demo_data_ran()
    {
        // CreateStandardSetAsync behaves as it does in production: nothing to create, because the
        // series were already seeded when the company itself was created.
        _series.CreateStandardSetAsync(_companyId, Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var existingUncommunicated = new SeriesListItemDto { Id = Guid.NewGuid(), CompanyId = _companyId, DocumentType = "FT", CanIssue = false };
        var alreadyCommunicated = new SeriesListItemDto { Id = Guid.NewGuid(), CompanyId = _companyId, DocumentType = "GR", CanIssue = true };

        _series.GetAllAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns([existingUncommunicated, alreadyCommunicated]);

        var result = await CreateService().ApplyAsync(_companyId, "user-1");

        result.SeriesCommunicated.Should().Be(1);
        await _series.Received(1).CommunicateManuallyAsync(existingUncommunicated.Id, "XXXX", Arg.Any<CancellationToken>());
        await _series.DidNotReceive().CommunicateManuallyAsync(alreadyCommunicated.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A battery is only a real, working battery line if it carries the eco-fee the law requires —
    /// the demo catalogue has to set this up the same way a real product editor would.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_links_battery_products_to_the_companys_battery_ecovalor()
    {
        var createdRequests = new List<CreateProductRequest>();
        _products.When(x => x.CreateAsync(Arg.Any<CreateProductRequest>(), Arg.Any<CancellationToken>()))
            .Do(call => createdRequests.Add(call.Arg<CreateProductRequest>()));

        await CreateService().ApplyAsync(_companyId, "user-1");

        createdRequests.Should().Contain(x => x.EcoFeeTypeId == _batteryEcoFeeTypeId, "some of the demo products are batteries");
        createdRequests.Should().Contain(x => x.EcoFeeTypeId == null, "not every demo product is a battery");
    }

    [Fact]
    public async Task ApplyAsync_does_nothing_when_demo_data_was_already_applied()
    {
        // "Travões" is the first family the demo catalogue creates — its presence is what marks a
        // company as already seeded, since codes are no longer a fingerprint of the demo data now
        // that families get the same sequential codes a hand-created one would.
        _families.GetAllAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns([new ProductFamilyDto(Guid.NewGuid(), "1", "Travões", true, 0)]);

        var result = await CreateService().ApplyAsync(_companyId, "user-1");

        result.Applied.Should().BeFalse();
        await _series.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
