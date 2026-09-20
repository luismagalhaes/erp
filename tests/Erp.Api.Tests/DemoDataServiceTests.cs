using Erp.Api.Services;
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
    private readonly ISeriesService _series = Substitute.For<ISeriesService>();

    private readonly Guid _companyId = Guid.NewGuid();

    public DemoDataServiceTests()
    {
        _families.CodeExistsAsync(_companyId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        _families.CreateAsync(Arg.Any<CreateProductFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProductFamilyDto(Guid.NewGuid(), call.Arg<CreateProductFamilyRequest>().Code, "Família", true, 0));

        _subfamilies.CreateAsync(Arg.Any<CreateProductSubfamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProductSubfamilyDto(
                Guid.NewGuid(), call.Arg<CreateProductSubfamilyRequest>().FamilyId, "Família", call.Arg<CreateProductSubfamilyRequest>().Code, "Subfamília", true));

        _brands.CreateAsync(Arg.Any<CreateBrandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new BrandDto(Guid.NewGuid(), call.Arg<CreateBrandRequest>().Code, call.Arg<CreateBrandRequest>().Name, true));

        _products.CreateAsync(Arg.Any<CreateProductRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProductListItemDto(
                Guid.NewGuid(), call.Arg<CreateProductRequest>().ProductCode, "Produto", "I", "UN", 10m, "NOR", 23m,
                barcode: null, familyId: null, familyName: null, subfamilyId: null, subfamilyName: null,
                brandId: null, brandName: null, isActive: true));
    }

    private DemoDataService CreateService() =>
        new(_families, _subfamilies, _brands, _products, _series, NullLogger<DemoDataService>.Instance);

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

    [Fact]
    public async Task ApplyAsync_does_nothing_when_demo_data_was_already_applied()
    {
        _families.CodeExistsAsync(_companyId, "DEMO-F01", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().ApplyAsync(_companyId, "user-1");

        result.Applied.Should().BeFalse();
        await _series.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
