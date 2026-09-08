using Erp.SeriesRegistry.Application.Services;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;
using Erp.Common;

namespace Erp.SeriesRegistry.Tests;

/// <summary>
/// What a series still allows to be changed once it exists. The list is short on purpose: the code,
/// the type and the numbering are woven into every document already issued, and the tax authority
/// has been told about them.
/// </summary>
public class SeriesUpdateTests
{
    private readonly ISeriesStorage _storage = Substitute.For<ISeriesStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Guid _companyId = Guid.NewGuid();

    private readonly List<Series> _stored = [];

    public SeriesUpdateTests()
    {
        _storage.When(x => x.AddAsync(Arg.Any<Series>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<Series>()));

        _storage.GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<Series>)[.. _stored]);

        _storage.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));
    }

    private SeriesService CreateService() => new(_storage, _unitOfWork);

    private async Task<SeriesListItemDto> GivenSeries(string documentType = "FT", string code = "FT2026") =>
        await CreateService().CreateAsync(new CreateSeriesRequest(_companyId, documentType, code), "user-1");

    [Fact]
    public async Task UpdateAsync_changes_the_stock_effect()
    {
        var series = await GivenSeries();
        series.StockEffect.Should().Be("Out");

        var updated = await CreateService().UpdateAsync(series.Id, new UpdateSeriesRequest("None"));

        updated!.StockEffect.Should().Be("None");
        _stored.Single().StockEffect.Should().Be(StockEffect.None);
    }

    /// <summary>
    /// The point of the whole endpoint: everything that identifies the series or its numbering is
    /// left exactly as it was, because the documents already issued say so.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_leaves_everything_fiscally_relevant_alone()
    {
        var series = await GivenSeries();
        await CreateService().CommunicateAsync(series.Id, "JFTX7RK9");

        var updated = await CreateService().UpdateAsync(series.Id, new UpdateSeriesRequest("In"));

        updated!.DocumentType.Should().Be("FT");
        updated.SeriesCode.Should().Be("FT2026");
        updated.ValidationCode.Should().Be("JFTX7RK9");
        updated.CurrentSequence.Should().Be(0);
        updated.CanIssue.Should().BeTrue("changing the stock effect does not disturb the numbering");
    }

    /// <summary>
    /// A communicated series is the normal case for this: the business realises its invoices should
    /// not move stock because a delivery note already did, and that has nothing to do with the AT.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_works_on_a_series_already_communicated()
    {
        var series = await GivenSeries();
        await CreateService().CommunicateAsync(series.Id, "JFTX7RK9");

        var updated = await CreateService().UpdateAsync(series.Id, new UpdateSeriesRequest("None"));

        updated!.StockEffect.Should().Be("None");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_a_series_that_does_not_exist()
    {
        var result = await CreateService().UpdateAsync(Guid.NewGuid(), new UpdateSeriesRequest("None"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_unknown_stock_effect()
    {
        var series = await GivenSeries();

        var act = () => CreateService().UpdateAsync(series.Id, new UpdateSeriesRequest("Sideways"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Sideways*");
    }

    /// <summary>
    /// On a create, saying nothing means "use the default for the type". On an update it means the
    /// field arrived empty, and quietly resetting the effect is not what the caller asked for.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_rejects_an_empty_stock_effect_instead_of_resetting_it()
    {
        var series = await GivenSeries();

        var act = () => CreateService().UpdateAsync(series.Id, new UpdateSeriesRequest("  "));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*stock effect is required*");
        _stored.Single().StockEffect.Should().Be(StockEffect.Out);
    }
}
