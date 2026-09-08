using Erp.SeriesRegistry.Application.Services;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;
using Erp.Common;

namespace Erp.SeriesRegistry.Tests;

/// <summary>
/// The series a company gets on the day it is created. Without them it cannot issue a single
/// document, because every document takes its number from a series.
/// </summary>
public class StandardSeriesTests
{
    private readonly ISeriesStorage _storage = Substitute.For<ISeriesStorage>();
    private readonly IErpUnitOfWork _unitOfWork = Substitute.For<IErpUnitOfWork>();
    private readonly Guid _companyId = Guid.NewGuid();

    private readonly List<Series> _stored = [];

    public StandardSeriesTests()
    {
        _storage.When(x => x.AddAsync(Arg.Any<Series>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<Series>()));

        _storage.GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<Series>)
                [.. _stored.Where(x => x.CompanyId == call.ArgAt<Guid>(0))]);
    }

    private SeriesService CreateService() => new(_storage, _unitOfWork);

    [Fact]
    public async Task CreateStandardSetAsync_creates_one_series_per_document_type()
    {
        var created = await CreateService().CreateStandardSetAsync(_companyId, 2026, "user-1");

        created.Should().HaveCount(StandardSeries.DocumentTypes.Length);
        created.Select(x => x.DocumentType).Should().BeEquivalentTo(StandardSeries.DocumentTypes);
    }

    /// <summary>
    /// The code carries the year because a series does not roll over: a new one is opened each
    /// year, and the code is what tells them apart at a glance.
    /// </summary>
    [Fact]
    public async Task CreateStandardSetAsync_codes_each_series_as_the_type_and_the_year()
    {
        var created = await CreateService().CreateStandardSetAsync(_companyId, 2026, "user-1");

        created.Single(x => x.DocumentType == "FT").SeriesCode.Should().Be("FT2026");
        created.Single(x => x.DocumentType == "NC").SeriesCode.Should().Be("NC2026");
        created.Single(x => x.DocumentType == "GR").SeriesCode.Should().Be("GR2026");
        created.Single(x => x.DocumentType == "RG").SeriesCode.Should().Be("RG2026");
    }

    /// <summary>
    /// Each type arrives with the stock effect its name implies — a delivery takes goods out, a
    /// credit note brings them back, a receipt moves money and not goods.
    /// </summary>
    [Theory]
    [InlineData("FT", "Out")]
    [InlineData("FS", "Out")]
    [InlineData("FR", "Out")]
    [InlineData("NC", "In")]
    [InlineData("ND", "None")]
    [InlineData("GR", "Out")]
    [InlineData("GT", "Out")]
    [InlineData("GC", "Out")]
    [InlineData("GD", "In")]
    [InlineData("GA", "None")]
    [InlineData("RC", "None")]
    [InlineData("RG", "None")]
    public async Task CreateStandardSetAsync_gives_each_type_its_usual_stock_effect(
        string documentType,
        string stockEffect)
    {
        var created = await CreateService().CreateStandardSetAsync(_companyId, 2026, "user-1");

        created.Single(x => x.DocumentType == documentType).StockEffect.Should().Be(stockEffect);
    }

    /// <summary>
    /// Seeding does not make a company able to issue: the tax authority still has to return a
    /// validation code for each series, and nobody can do that on the company's behalf.
    /// </summary>
    [Fact]
    public async Task CreateStandardSetAsync_leaves_every_series_unable_to_issue()
    {
        var created = await CreateService().CreateStandardSetAsync(_companyId, 2026, "user-1");

        created.Should().OnlyContain(x => !x.CanIssue);
        created.Should().OnlyContain(x => x.ValidationCode == null);
    }

    [Fact]
    public async Task CreateStandardSetAsync_running_twice_creates_nothing_the_second_time()
    {
        var service = CreateService();

        await service.CreateStandardSetAsync(_companyId, 2026, "user-1");
        var again = await service.CreateStandardSetAsync(_companyId, 2026, "user-1");

        again.Should().BeEmpty();
        _stored.Should().HaveCount(StandardSeries.DocumentTypes.Length);
    }

    /// <summary>A series created by hand is left alone — its numbers are why it cannot be replaced.</summary>
    [Fact]
    public async Task CreateStandardSetAsync_leaves_an_existing_series_untouched()
    {
        var service = CreateService();

        await service.CreateAsync(new CreateSeriesRequest(_companyId, "FT", "FT2026", StockEffect: "None"), "user-1");

        var created = await service.CreateStandardSetAsync(_companyId, 2026, "user-1");

        created.Should().NotContain(x => x.DocumentType == "FT");
        _stored.Single(x => x.DocumentType == "FT").StockEffect.Should().Be(Domain.StockEffect.None);
    }

    /// <summary>A different year is a different set, so opening 2027 adds to 2026 rather than clashing.</summary>
    [Fact]
    public async Task CreateStandardSetAsync_treats_each_year_as_its_own_set()
    {
        var service = CreateService();

        await service.CreateStandardSetAsync(_companyId, 2026, "user-1");
        var next = await service.CreateStandardSetAsync(_companyId, 2027, "user-1");

        next.Should().HaveCount(StandardSeries.DocumentTypes.Length);
        next.Single(x => x.DocumentType == "FT").SeriesCode.Should().Be("FT2027");
    }

    [Fact]
    public async Task CreateStandardSetAsync_gives_each_company_its_own_set()
    {
        var service = CreateService();
        var other = Guid.NewGuid();

        await service.CreateStandardSetAsync(_companyId, 2026, "user-1");
        var created = await service.CreateStandardSetAsync(other, 2026, "user-1");

        created.Should().HaveCount(StandardSeries.DocumentTypes.Length);
        created.Should().OnlyContain(x => x.CompanyId == other);
    }

    /// <summary>Nothing is written when there is nothing to write.</summary>
    [Fact]
    public async Task CreateStandardSetAsync_does_not_save_when_everything_already_exists()
    {
        var service = CreateService();
        await service.CreateStandardSetAsync(_companyId, 2026, "user-1");

        _unitOfWork.ClearReceivedCalls();

        await service.CreateStandardSetAsync(_companyId, 2026, "user-1");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
